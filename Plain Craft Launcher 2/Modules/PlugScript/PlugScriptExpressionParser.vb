' PlugScript 表达式解析器（优先级爬升）。
Option Strict Off

Imports System.Collections.Generic

Friend Class PsExpressionParser

    Private ReadOnly _tokens As List(Of PsToken)
    Private ReadOnly _line As Integer
    Private ReadOnly _islands As Dictionary(Of String, Object)
    Private _index As Integer

    Public Sub New(Text As String, Line As Integer, Optional Islands As Dictionary(Of String, Object) = Nothing)
        _line = Line
        _islands = If(Islands, New Dictionary(Of String, Object)())
        _tokens = New PsLexer(Text, Line).Tokenize()
    End Sub

    Public Function Parse() As PsExpr
        Dim Result = ParseOr()
        If Current.Kind <> PsTokenKind.EndOfInput Then
            Throw New PsScriptException($"表达式末尾存在无法识别的内容「{Current.Text}」。", _line)
        End If
        Return Result
    End Function

    Private ReadOnly Property Current As PsToken
        Get
            If _index >= _tokens.Count Then Return _tokens(_tokens.Count - 1)
            Return _tokens(_index)
        End Get
    End Property

    Private Function Peek(Offset As Integer) As PsToken
        Dim Position = _index + Offset
        If Position < 0 OrElse Position >= _tokens.Count Then Return _tokens(_tokens.Count - 1)
        Return _tokens(Position)
    End Function

    Private Function MatchOperator(ParamArray Values As String()) As Boolean
        If Current.Kind <> PsTokenKind.OperatorToken AndAlso Current.Kind <> PsTokenKind.Identifier AndAlso Current.Kind <> PsTokenKind.Punctuation Then Return False
        For Each Value In Values
            If String.Equals(Current.Text, Value, StringComparison.OrdinalIgnoreCase) Then
                _index += 1
                Return True
            End If
        Next
        Return False
    End Function

    Private Function MatchPunctuation(Value As String) As Boolean
        If Current.Kind = PsTokenKind.Punctuation AndAlso Current.Text = Value Then
            _index += 1
            Return True
        End If
        Return False
    End Function

    Private Sub ExpectPunctuation(Value As String)
        If Not MatchPunctuation(Value) Then
            Throw New PsScriptException($"表达式缺少「{Value}」，实际为「{Current.Text}」。", _line)
        End If
    End Sub

    Private Function ParseOr() As PsExpr
        Dim Left = ParseAnd()
        Do
            If Not MatchOperator("||", "or") Then Exit Do
            Dim Right = ParseAnd()
            Left = New PsBinaryExpr With {.Line = _line, .Left = Left, .Operator = "||", .Right = Right}
        Loop
        Return Left
    End Function

    Private Function ParseAnd() As PsExpr
        Dim Left = ParseEquality()
        Do
            If Not MatchOperator("&&", "and") Then Exit Do
            Dim Right = ParseEquality()
            Left = New PsBinaryExpr With {.Line = _line, .Left = Left, .Operator = "&&", .Right = Right}
        Loop
        Return Left
    End Function

    Private Function ParseEquality() As PsExpr
        Dim Left = ParseComparison()
        Do
            Dim OperatorText As String = Nothing
            If MatchOperator("==") Then
                OperatorText = "=="
            ElseIf MatchOperator("!=") Then
                OperatorText = "!="
            Else
                Exit Do
            End If
            Dim Right = ParseComparison()
            Left = New PsBinaryExpr With {.Line = _line, .Left = Left, .Operator = OperatorText, .Right = Right}
        Loop
        Return Left
    End Function

    Private Function ParseComparison() As PsExpr
        Dim Left = ParseAdditive()
        Do
            Dim OperatorText As String = Nothing
            If MatchOperator(">=") Then
                OperatorText = ">="
            ElseIf MatchOperator("<=") Then
                OperatorText = "<="
            ElseIf MatchOperator(">") Then
                OperatorText = ">"
            ElseIf MatchOperator("<") Then
                OperatorText = "<"
            Else
                Exit Do
            End If
            Dim Right = ParseAdditive()
            Left = New PsBinaryExpr With {.Line = _line, .Left = Left, .Operator = OperatorText, .Right = Right}
        Loop
        Return Left
    End Function

    Private Function ParseAdditive() As PsExpr
        Dim Left = ParseMultiplicative()
        Do
            Dim OperatorText As String = Nothing
            If MatchOperator("+") Then
                OperatorText = "+"
            ElseIf MatchOperator("-") Then
                OperatorText = "-"
            Else
                Exit Do
            End If
            Dim Right = ParseMultiplicative()
            Left = New PsBinaryExpr With {.Line = _line, .Left = Left, .Operator = OperatorText, .Right = Right}
        Loop
        Return Left
    End Function

    Private Function ParseMultiplicative() As PsExpr
        Dim Left = ParseUnary()
        Do
            Dim OperatorText As String = Nothing
            If MatchOperator("*") Then
                OperatorText = "*"
            ElseIf MatchOperator("/") Then
                OperatorText = "/"
            ElseIf MatchOperator("%") Then
                OperatorText = "%"
            Else
                Exit Do
            End If
            Dim Right = ParseUnary()
            Left = New PsBinaryExpr With {.Line = _line, .Left = Left, .Operator = OperatorText, .Right = Right}
        Loop
        Return Left
    End Function

    Private Function ParseUnary() As PsExpr
        If Current.Kind = PsTokenKind.OperatorToken AndAlso (Current.Text = "-" OrElse Current.Text = "+" OrElse Current.Text = "!") Then
            Dim OperatorText = Current.Text
            _index += 1
            Return New PsUnaryExpr With {.Line = _line, .Operator = OperatorText, .Operand = ParseUnary()}
        End If
        If Current.Kind = PsTokenKind.Identifier AndAlso String.Equals(Current.Text, "not", StringComparison.OrdinalIgnoreCase) Then
            _index += 1
            Return New PsUnaryExpr With {.Line = _line, .Operator = "not", .Operand = ParseUnary()}
        End If
        Return ParsePostfix()
    End Function

    Private Function ParsePostfix() As PsExpr
        Dim Result = ParsePrimary()
        Do
            If MatchPunctuation(".") Then
                If Current.Kind <> PsTokenKind.Identifier Then
                    Throw New PsScriptException("成员访问符「.」后缺少名称。", _line)
                End If
                Dim MemberName = Current.Text
                _index += 1
                Result = New PsMemberExpr With {.Line = _line, .Target = Result, .MemberName = MemberName}
            ElseIf MatchPunctuation("(") Then
                Dim CallNode As New PsCallExpr With {.Line = _line, .Callee = Result}
                If Not MatchPunctuation(")") Then
                    Do
                        CallNode.Arguments.Add(ParseOr())
                        If MatchPunctuation(")") Then Exit Do
                        ExpectPunctuation(",")
                    Loop
                End If
                Result = CallNode
            ElseIf MatchPunctuation("[") Then
                Dim IndexExpression = ParseOr()
                ExpectPunctuation("]")
                Result = New PsIndexExpr With {.Line = _line, .Target = Result, .Index = IndexExpression}
            Else
                Exit Do
            End If
        Loop
        Return Result
    End Function

    Private Function ParsePrimary() As PsExpr
        Dim Token = Current

        If Token.Kind = PsTokenKind.Number OrElse Token.Kind = PsTokenKind.StringLiteral Then
            _index += 1
            Return New PsLiteralExpr With {.Line = _line, .Value = Token.Value}
        End If

        If Token.Kind = PsTokenKind.Identifier Then
            _index += 1
            If _islands.ContainsKey(Token.Text) Then
                Return New PsLiteralExpr With {.Line = _line, .Value = _islands(Token.Text)}
            End If
            If String.Equals(Token.Text, "true", StringComparison.OrdinalIgnoreCase) Then
                Return New PsLiteralExpr With {.Line = _line, .Value = True}
            End If
            If String.Equals(Token.Text, "false", StringComparison.OrdinalIgnoreCase) Then
                Return New PsLiteralExpr With {.Line = _line, .Value = False}
            End If
            If String.Equals(Token.Text, "nothing", StringComparison.OrdinalIgnoreCase) OrElse
               String.Equals(Token.Text, "null", StringComparison.OrdinalIgnoreCase) Then
                Return New PsLiteralExpr With {.Line = _line, .Value = Nothing}
            End If
            Return New PsIdentifierExpr With {.Line = _line, .Name = Token.Text}
        End If

        If MatchPunctuation("(") Then
            Dim Result = ParseOr()
            ExpectPunctuation(")")
            Return Result
        End If

        If MatchPunctuation("[") Then
            Dim ArrayNode As New PsArrayExpr With {.Line = _line}
            If Not MatchPunctuation("]") Then
                Do
                    ArrayNode.Items.Add(ParseOr())
                    If MatchPunctuation("]") Then Exit Do
                    ExpectPunctuation(",")
                Loop
            End If
            Return ArrayNode
        End If

        Throw New PsScriptException($"表达式中出现无法识别的内容「{Token.Text}」。", _line)
    End Function

End Class
