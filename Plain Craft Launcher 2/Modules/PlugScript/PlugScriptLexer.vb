' PlugScript 的词法分析与源码文本扫描工具。
Option Strict Off

Imports System.Collections.Generic
Imports System.Globalization
Imports System.Text

Friend Enum PsTokenKind
    EndOfInput
    Number
    StringLiteral
    Identifier
    OperatorToken
    Punctuation
End Enum

Friend Class PsToken
    Public Property Kind As PsTokenKind
    Public Property Text As String
    Public Property Value As Object
    Public Property Position As Integer
End Class

''' <summary>把一段表达式文本拆分为 Token。语句结构由行解析器负责。</summary>
Friend Class PsLexer

    Private ReadOnly _text As String
    Private ReadOnly _line As Integer
    Private _position As Integer

    Public Sub New(Text As String, Line As Integer)
        _text = If(Text, "")
        _line = Line
    End Sub

    Public Function Tokenize() As List(Of PsToken)
        Dim Result As New List(Of PsToken)()
        Do
            SkipWhitespaceAndComments()
            If _position >= _text.Length Then
                Result.Add(New PsToken With {.Kind = PsTokenKind.EndOfInput, .Text = "", .Position = _position})
                Exit Do
            End If

            Dim Current = _text(_position)
            If Current = """"c OrElse Current = "'"c Then
                Result.Add(ReadString(Current))
            ElseIf Char.IsDigit(Current) OrElse (Current = "."c AndAlso _position + 1 < _text.Length AndAlso Char.IsDigit(_text(_position + 1))) Then
                Result.Add(ReadNumber())
            ElseIf IsIdentifierStart(Current) Then
                Result.Add(ReadIdentifier())
            Else
                Result.Add(ReadOperatorOrPunctuation())
            End If
        Loop
        Return Result
    End Function

    Private Sub SkipWhitespaceAndComments()
        Do
            While _position < _text.Length AndAlso Char.IsWhiteSpace(_text(_position))
                _position += 1
            End While
            If _position + 1 < _text.Length AndAlso _text(_position) = "/"c AndAlso _text(_position + 1) = "/"c Then
                _position = _text.Length
                Return
            End If
            Exit Do
        Loop
    End Sub

    Private Function ReadString(Quote As Char) As PsToken
        Dim Start = _position
        _position += 1
        Dim Builder As New StringBuilder()
        While _position < _text.Length
            Dim Current = _text(_position)
            If Current = Quote Then
                _position += 1
                Return New PsToken With {
                    .Kind = PsTokenKind.StringLiteral,
                    .Text = _text.Substring(Start, _position - Start),
                    .Value = Builder.ToString(),
                    .Position = Start
                }
            End If
            If Current = "\"c Then
                _position += 1
                If _position >= _text.Length Then Throw New PsScriptException("字符串末尾存在未完成的转义。", _line)
                Dim Escaped = _text(_position)
                Select Case Escaped
                    Case "n"c : Builder.Append(vbLf)
                    Case "r"c : Builder.Append(vbCr)
                    Case "t"c : Builder.Append(vbTab)
                    Case """"c : Builder.Append(""""c)
                    Case "'"c : Builder.Append("'"c)
                    Case "\"c : Builder.Append("\"c)
                    Case Else : Builder.Append(Escaped)
                End Select
                _position += 1
            Else
                Builder.Append(Current)
                _position += 1
            End If
        End While
        Throw New PsScriptException("字符串缺少结束引号。", _line)
    End Function

    Private Function ReadNumber() As PsToken
        Dim Start = _position
        Dim HasDot = False
        Dim HasExponent = False

        While _position < _text.Length
            Dim Current = _text(_position)
            If Char.IsDigit(Current) Then
                _position += 1
            ElseIf Current = "."c AndAlso Not HasDot AndAlso Not HasExponent Then
                HasDot = True
                _position += 1
            ElseIf (Current = "e"c OrElse Current = "E"c) AndAlso Not HasExponent Then
                HasExponent = True
                _position += 1
                If _position < _text.Length AndAlso (_text(_position) = "+"c OrElse _text(_position) = "-"c) Then _position += 1
            Else
                Exit While
            End If
        End While

        Dim Raw = _text.Substring(Start, _position - Start)
        Dim Value As Object
        Dim AsLong As Long
        If Not HasDot AndAlso Not HasExponent AndAlso Long.TryParse(Raw, NumberStyles.Integer, CultureInfo.InvariantCulture, AsLong) Then
            Value = AsLong
        Else
            Dim AsDouble As Double
            If Not Double.TryParse(Raw, NumberStyles.Float, CultureInfo.InvariantCulture, AsDouble) Then
                Throw New PsScriptException($"数字「{Raw}」格式不正确。", _line)
            End If
            Value = AsDouble
        End If
        Return New PsToken With {.Kind = PsTokenKind.Number, .Text = Raw, .Value = Value, .Position = Start}
    End Function

    Private Function ReadIdentifier() As PsToken
        Dim Start = _position
        _position += 1
        While _position < _text.Length AndAlso IsIdentifierPart(_text(_position))
            _position += 1
        End While
        Dim Raw = _text.Substring(Start, _position - Start)
        Return New PsToken With {.Kind = PsTokenKind.Identifier, .Text = Raw, .Value = Raw, .Position = Start}
    End Function

    Private Function ReadOperatorOrPunctuation() As PsToken
        Dim Start = _position
        Dim Two = If(_position + 1 < _text.Length, _text.Substring(_position, 2), "")
        If Two = "==" OrElse Two = "!=" OrElse Two = ">=" OrElse Two = "<=" OrElse Two = "&&" OrElse Two = "||" Then
            _position += 2
            Return New PsToken With {.Kind = PsTokenKind.OperatorToken, .Text = Two, .Position = Start}
        End If

        Dim Current = _text(_position)
        _position += 1
        If "+-*/%><!&|=".IndexOf(Current) >= 0 Then
            Return New PsToken With {.Kind = PsTokenKind.OperatorToken, .Text = Current.ToString(), .Position = Start}
        End If
        If "()[]{},:.".IndexOf(Current) >= 0 Then
            Return New PsToken With {.Kind = PsTokenKind.Punctuation, .Text = Current.ToString(), .Position = Start}
        End If
        Throw New PsScriptException($"无法识别的字符「{Current}」。", _line)
    End Function

    Private Shared Function IsIdentifierStart(Value As Char) As Boolean
        Return Char.IsLetter(Value) OrElse Value = "_"c
    End Function

    Private Shared Function IsIdentifierPart(Value As Char) As Boolean
        Return Char.IsLetterOrDigit(Value) OrElse Value = "_"c
    End Function

End Class

''' <summary>源码文本扫描工具，所有方法都忽略字符串字面量中的符号。</summary>
Friend Module PsText

    Public Function StripComment(Text As String) As String
        If String.IsNullOrEmpty(Text) Then Return ""
        Dim Quote As Char = ChrW(0)
        Dim Escaped As Boolean = False
        For Index = 0 To Text.Length - 1
            Dim Current = Text(Index)
            If Quote <> ChrW(0) Then
                If Escaped Then
                    Escaped = False
                ElseIf Current = "\"c Then
                    Escaped = True
                ElseIf Current = Quote Then
                    Quote = ChrW(0)
                End If
            Else
                If Current = """"c OrElse Current = "'"c Then
                    Quote = Current
                ElseIf Current = "/"c AndAlso Index + 1 < Text.Length AndAlso Text(Index + 1) = "/"c Then
                    Return Text.Substring(0, Index)
                End If
            End If
        Next
        Return Text
    End Function

    Public Function StripTrailingSemicolon(Text As String) As String
        Dim Result = If(Text, "")
        Do
            Dim LastIndex As Integer = -1
            Dim Quote As Char = ChrW(0)
            Dim Escaped As Boolean = False
            For Index = 0 To Result.Length - 1
                Dim Current = Result(Index)
                If Quote <> ChrW(0) Then
                    If Escaped Then
                        Escaped = False
                    ElseIf Current = "\"c Then
                        Escaped = True
                    ElseIf Current = Quote Then
                        Quote = ChrW(0)
                    End If
                Else
                    If Current = """"c OrElse Current = "'"c Then
                        Quote = Current
                    ElseIf Not Char.IsWhiteSpace(Current) Then
                        LastIndex = Index
                    End If
                End If
            Next
            If LastIndex >= 0 AndAlso Result(LastIndex) = ";"c Then
                Result = Result.Substring(0, LastIndex) & Result.Substring(LastIndex + 1)
            Else
                Return Result
            End If
        Loop
    End Function

    ''' <summary>查找指定单词，忽略字符串内部，要求两侧是标识符边界。</summary>
    Public Function FindWord(Text As String, Word As String, StartAt As Integer) As Integer
        Dim Quote As Char = ChrW(0)
        Dim Escaped As Boolean = False
        Dim Index = Math.Max(0, StartAt)
        While Index <= Text.Length - Word.Length
            Dim Current = Text(Index)
            If Quote <> ChrW(0) Then
                If Escaped Then
                    Escaped = False
                ElseIf Current = "\"c Then
                    Escaped = True
                ElseIf Current = Quote Then
                    Quote = ChrW(0)
                End If
                Index += 1
                Continue While
            End If
            If Current = """"c OrElse Current = "'"c Then
                Quote = Current
                Index += 1
                Continue While
            End If
            If String.Compare(Text, Index, Word, 0, Word.Length, StringComparison.OrdinalIgnoreCase) = 0 Then
                Dim BeforeOk = Index = 0 OrElse Not IsIdentifierChar(Text(Index - 1))
                Dim AfterIndex = Index + Word.Length
                Dim AfterOk = AfterIndex >= Text.Length OrElse Not IsIdentifierChar(Text(AfterIndex))
                If BeforeOk AndAlso AfterOk Then Return Index
            End If
            Index += 1
        End While
        Return -1
    End Function

    Public Function IsIdentifierChar(Value As Char) As Boolean
        Return Char.IsLetterOrDigit(Value) OrElse Value = "_"c
    End Function

    Public Function IsValidIdentifier(Text As String) As Boolean
        If String.IsNullOrEmpty(Text) Then Return False
        If Not (Char.IsLetter(Text(0)) OrElse Text(0) = "_"c) Then Return False
        For Index = 1 To Text.Length - 1
            If Not IsIdentifierChar(Text(Index)) Then Return False
        Next
        Return True
    End Function

    ''' <summary>在字符串与括号之外查找字符，返回 -1 表示未找到。</summary>
    Public Function FindTopLevelChar(Text As String, Target As Char) As Integer
        Dim Quote As Char = ChrW(0)
        Dim Escaped As Boolean = False
        Dim Parentheses = 0
        Dim Brackets = 0
        Dim Braces = 0
        For Index = 0 To Text.Length - 1
            Dim Current = Text(Index)
            If Quote <> ChrW(0) Then
                If Escaped Then
                    Escaped = False
                ElseIf Current = "\"c Then
                    Escaped = True
                ElseIf Current = Quote Then
                    Quote = ChrW(0)
                End If
            Else
                Select Case Current
                    Case """"c, "'"c
                        Quote = Current
                    Case "("c
                        Parentheses += 1
                    Case ")"c
                        Parentheses -= 1
                    Case "["c
                        Brackets += 1
                    Case "]"c
                        Brackets -= 1
                    Case "{"c
                        Braces += 1
                    Case "}"c
                        Braces -= 1
                    Case Else
                        If Current = Target AndAlso Parentheses = 0 AndAlso Brackets = 0 AndAlso Braces = 0 Then Return Index
                End Select
            End If
        Next
        Return -1
    End Function

    ''' <summary>查找赋值等号，排除 ==、!=、&lt;=、&gt;= 等比较运算符。</summary>
    Public Function FindAssignmentEquals(Text As String) As Integer
        Dim Quote As Char = ChrW(0)
        Dim Escaped As Boolean = False
        Dim Parentheses = 0
        Dim Brackets = 0
        For Index = 0 To Text.Length - 1
            Dim Current = Text(Index)
            If Quote <> ChrW(0) Then
                If Escaped Then
                    Escaped = False
                ElseIf Current = "\"c Then
                    Escaped = True
                ElseIf Current = Quote Then
                    Quote = ChrW(0)
                End If
            Else
                Select Case Current
                    Case """"c, "'"c
                        Quote = Current
                    Case "("c
                        Parentheses += 1
                    Case ")"c
                        Parentheses -= 1
                    Case "["c
                        Brackets += 1
                    Case "]"c
                        Brackets -= 1
                    Case "="c
                        If Parentheses = 0 AndAlso Brackets = 0 Then
                            Dim Previous = If(Index > 0, Text(Index - 1), ChrW(0))
                            Dim [Next] = If(Index + 1 < Text.Length, Text(Index + 1), ChrW(0))
                            If [Next] <> "="c AndAlso "=!<>".IndexOf(Previous) < 0 Then Return Index
                        End If
                End Select
            End If
        Next
        Return -1
    End Function

    Public Function SplitTopLevel(Text As String, Separator As Char) As List(Of String)
        Dim Result As New List(Of String)()
        Dim Quote As Char = ChrW(0)
        Dim Escaped As Boolean = False
        Dim Parentheses = 0
        Dim Brackets = 0
        Dim Braces = 0
        Dim Start = 0
        For Index = 0 To Text.Length - 1
            Dim Current = Text(Index)
            If Quote <> ChrW(0) Then
                If Escaped Then
                    Escaped = False
                ElseIf Current = "\"c Then
                    Escaped = True
                ElseIf Current = Quote Then
                    Quote = ChrW(0)
                End If
            Else
                Select Case Current
                    Case """"c, "'"c
                        Quote = Current
                    Case "("c
                        Parentheses += 1
                    Case ")"c
                        Parentheses -= 1
                    Case "["c
                        Brackets += 1
                    Case "]"c
                        Brackets -= 1
                    Case "{"c
                        Braces += 1
                    Case "}"c
                        Braces -= 1
                    Case Else
                        If Current = Separator AndAlso Parentheses = 0 AndAlso Brackets = 0 AndAlso Braces = 0 Then
                            Result.Add(Text.Substring(Start, Index - Start).Trim())
                            Start = Index + 1
                        End If
                End Select
            End If
        Next
        Result.Add(Text.Substring(Start).Trim())
        Return Result
    End Function

    ''' <summary>统计一行中未闭合的大括号数量，字符串内的括号忽略。</summary>
    Public Function CountBraceDelta(Text As String) As Integer
        Dim Delta = 0
        Dim Quote As Char = ChrW(0)
        Dim Escaped As Boolean = False
        For Index = 0 To Text.Length - 1
            Dim Current = Text(Index)
            If Quote <> ChrW(0) Then
                If Escaped Then
                    Escaped = False
                ElseIf Current = "\"c Then
                    Escaped = True
                ElseIf Current = Quote Then
                    Quote = ChrW(0)
                End If
            Else
                Select Case Current
                    Case """"c, "'"c
                        Quote = Current
                    Case "{"c
                        Delta += 1
                    Case "}"c
                        Delta -= 1
                End Select
            End If
        Next
        Return Delta
    End Function

    ''' <summary>返回一行中让大括号深度归零的那个 } 之后的内容。</summary>
    Public Function TextAfterClosingBrace(Text As String, CurrentDepth As Integer) As String
        Dim Quote As Char = ChrW(0)
        Dim Escaped As Boolean = False
        Dim Depth = CurrentDepth
        For Index = 0 To Text.Length - 1
            Dim Current = Text(Index)
            If Quote <> ChrW(0) Then
                If Escaped Then
                    Escaped = False
                ElseIf Current = "\"c Then
                    Escaped = True
                ElseIf Current = Quote Then
                    Quote = ChrW(0)
                End If
            Else
                Select Case Current
                    Case """"c, "'"c
                        Quote = Current
                    Case "{"c
                        Depth += 1
                    Case "}"c
                        Depth -= 1
                        If Depth <= 0 Then Return Text.Substring(Index + 1).Trim()
                End Select
            End If
        Next
        Return ""
    End Function

End Module
