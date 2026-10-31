' PlugScript 的源码行解析器：语句、代码块与 island 结构。
Option Strict Off

Imports System.Collections.Generic
Imports System.Globalization
Imports System.Text
Imports IDictionary = System.Collections.IDictionary

''' <summary>预处理后的源码行，保留原始行号与缩进。</summary>
Friend Class PsSourceLine
    Public Property Text As String
    Public Property Indent As Integer
    Public Property Number As Integer
End Class

Friend Class PsIslandCapture
    Public Property Lines As New List(Of PsSourceLine)()
    Public Property Remainder As String = ""
    Public Property NextIndex As Integer
End Class

''' <summary>把源码解析为 PsProgram。支持大括号块与纯缩进块。</summary>
Friend Class PsParser

    Private _lines As New List(Of PsSourceLine)()
    Private _index As Integer
    Private _braceRemainder As String = ""
    Private _afterSimpleStatement As Integer
    Private _islandValues As New Dictionary(Of String, Object)()
    Private _islandCounter As Integer

    Public Function Parse(Source As String) As PsProgram
        _lines = Preprocess(Source)
        _index = 0
        _islandValues = New Dictionary(Of String, Object)()
        _islandCounter = 0

        Dim Program As New PsProgram()
        Program.Statements = ParseStatementList(-1, False, True, 0)

        For Each Statement In Program.Statements
            Dim FunctionStatement = TryCast(Statement, PsFunctionStmt)
            If FunctionStatement IsNot Nothing Then
                Program.Functions(FunctionStatement.Name) = FunctionStatement
            End If
        Next
        Return Program
    End Function

    Private Shared Function Preprocess(Source As String) As List(Of PsSourceLine)
        Dim Result As New List(Of PsSourceLine)()
        If Source Is Nothing Then Return Result

        Dim Normalized = Source.Replace(vbCrLf, vbLf).Replace(vbCr, vbLf)
        Dim RawLines = Normalized.Split(New String() {vbLf}, StringSplitOptions.None)
        For Index = 0 To RawLines.Length - 1
            Dim WithoutComment = PsText.StripComment(RawLines(Index))
            If String.IsNullOrWhiteSpace(WithoutComment) Then Continue For

            Dim Text = PsText.StripTrailingSemicolon(WithoutComment.Trim())
            If Text.Length = 0 Then Continue For

            Dim Indent = 0
            For Each Character In WithoutComment
                If Character = " "c Then
                    Indent += 1
                ElseIf Character = vbTab Then
                    Indent += 4
                Else
                    Exit For
                End If
            Next

            Result.Add(New PsSourceLine With {
                .Text = Text,
                .Indent = Indent,
                .Number = Index + 1
            })
        Next
        Return Result
    End Function

    Private Function ParseStatementList(ParentIndent As Integer, StopAtBrace As Boolean, IsTopLevel As Boolean, OpeningLine As Integer) As List(Of PsStmt)
        Dim Result As New List(Of PsStmt)()
        While _index < _lines.Count
            Dim CurrentLine = _lines(_index)

            If StopAtBrace AndAlso CurrentLine.Text.StartsWith("}") Then
                _index += 1
                _braceRemainder = PsText.StripTrailingSemicolon(CurrentLine.Text.Substring(1).Trim())
                Return Result
            End If

            If Not IsTopLevel AndAlso CurrentLine.Indent <= ParentIndent Then Return Result
            If CurrentLine.Text.StartsWith("}") Then
                Throw New PsScriptException("出现了没有对应左大括号的「}」。", CurrentLine.Number)
            End If
            If StartsWord(CurrentLine.Text, "else") Then
                Throw New PsScriptException("「else」没有对应的「if」。", CurrentLine.Number)
            End If

            Result.Add(ParseStatement())
        End While

        If StopAtBrace Then
            Throw New PsScriptException("代码块缺少右大括号「}」。", OpeningLine)
        End If
        Return Result
    End Function

    Private Function ParseStatement() As PsStmt
        Dim Opening = _lines(_index)
        _afterSimpleStatement = _index + 1
        _islandValues = New Dictionary(Of String, Object)()

        Dim ExpandedText = ExpandIslands(Opening, _index)
        Dim Current = New PsSourceLine With {
            .Text = ExpandedText,
            .Indent = Opening.Indent,
            .Number = Opening.Number
        }

        If StartsWord(Current.Text, "function") Then Return ParseFunction(Current)
        If StartsWord(Current.Text, "if") Then Return ParseIf(Current)
        If StartsWord(Current.Text, "while") Then Return ParseWhile(Current)
        If StartsWord(Current.Text, "return") Then Return ParseReturn(Current)
        If StartsWord(Current.Text, "let") OrElse StartsWord(Current.Text, "const") Then Return ParseLet(Current)

        Return ParseSimple(Current)
    End Function

    Private Shared Function StartsWord(Text As String, Word As String) As Boolean
        If Text Is Nothing Then Return False
        Return PsText.FindWord(Text, Word, 0) = 0
    End Function

    ''' <summary>把语句中的 island {...} 或 island + 缩进块替换为字面量标记。</summary>
    Private Function ExpandIslands(Line As PsSourceLine, LineIndex As Integer) As String
        Dim Text = Line.Text
        Dim Search = 0
        Dim Builder As New StringBuilder()

        Do
            Dim Found = PsText.FindWord(Text, "island", Search)
            If Found < 0 Then
                Builder.Append(Text.Substring(Search))
                Exit Do
            End If

            Builder.Append(Text.Substring(Search, Found - Search))
            Dim After = Text.Substring(Found + "island".Length).TrimStart()

            If After.StartsWith("{") Then
                Dim BraceIndex = Text.IndexOf("{"c, Found + "island".Length)
                Dim RemainderOnLine = Text.Substring(BraceIndex + 1).Trim()
                If RemainderOnLine.Length > 0 Then
                    Throw New PsScriptException("暂不支持在同一行内书写 island 内容。", Line.Number)
                End If

                Dim Capture = CaptureBraceIsland(LineIndex, Line.Number)
                Dim Marker = NewIslandMarker()
                _islandValues(Marker) = ParseIslandBody(Capture.Lines, Line.Number)
                Builder.Append(Marker)
                If Capture.Remainder.Length > 0 Then
                    Builder.Append(" ").Append(PsText.StripTrailingSemicolon(Capture.Remainder))
                End If
                _afterSimpleStatement = Capture.NextIndex
                Search = Text.Length
            ElseIf After.Length = 0 Then
                Dim Capture = CaptureIndentedIsland(LineIndex, Line.Indent)
                Dim Marker = NewIslandMarker()
                _islandValues(Marker) = ParseIslandBody(Capture.Lines, Line.Number)
                Builder.Append(Marker)
                _afterSimpleStatement = Capture.NextIndex
                Search = Text.Length
            Else
                ' 「island」只是普通标识符的一部分或普通名称。
                Builder.Append("island")
                Search = Found + "island".Length
            End If
        Loop

        Return PsText.StripTrailingSemicolon(Builder.ToString())
    End Function

    Private Function NewIslandMarker() As String
        _islandCounter += 1
        Return "__ps_island_" & _islandCounter.ToString(CultureInfo.InvariantCulture)
    End Function

    Private Function CaptureBraceIsland(LineIndex As Integer, OpeningLine As Integer) As PsIslandCapture
        Dim Capture As New PsIslandCapture()
        Dim Depth = 1
        Dim Index = LineIndex + 1

        While Index < _lines.Count
            Dim CurrentLine = _lines(Index)
            Dim Delta = PsText.CountBraceDelta(CurrentLine.Text)

            If Depth + Delta <= 0 Then
                Capture.Remainder = PsText.TextAfterClosingBrace(CurrentLine.Text, Depth)
                Capture.NextIndex = Index + 1
                Return Capture
            End If

            Capture.Lines.Add(CurrentLine)
            Depth += Delta
            Index += 1
        End While

        Throw New PsScriptException("island 缺少右大括号「}」。", OpeningLine)
    End Function

    Private Function CaptureIndentedIsland(LineIndex As Integer, ParentIndent As Integer) As PsIslandCapture
        Dim Capture As New PsIslandCapture()
        Dim Index = LineIndex + 1
        While Index < _lines.Count AndAlso _lines(Index).Indent > ParentIndent
            Capture.Lines.Add(_lines(Index))
            Index += 1
        End While
        Capture.NextIndex = Index
        Return Capture
    End Function

    Private Function ParseFunction(Opening As PsSourceLine) As PsStmt
        Dim Rest = Opening.Text.Substring("function".Length).Trim()
        If Rest.Length = 0 Then Throw New PsScriptException("function 后缺少函数名。", Opening.Number)

        Dim NameLength = 0
        While NameLength < Rest.Length AndAlso PsText.IsIdentifierChar(Rest(NameLength))
            NameLength += 1
        End While
        Dim Name = Rest.Substring(0, NameLength)
        If Not PsText.IsValidIdentifier(Name) Then
            Throw New PsScriptException($"函数名「{Name}」不合法。", Opening.Number)
        End If

        Rest = Rest.Substring(NameLength).TrimStart()
        If Not Rest.StartsWith("(") Then
            Throw New PsScriptException("函数声明缺少参数列表「(」。", Opening.Number)
        End If
        Dim CloseIndex = Rest.IndexOf(")"c)
        If CloseIndex < 0 Then
            Throw New PsScriptException("函数声明的参数列表缺少「)」。", Opening.Number)
        End If

        Dim ParameterText = Rest.Substring(1, CloseIndex - 1).Trim()
        Dim Parameters As New List(Of String)()
        If ParameterText.Length > 0 Then
            For Each Part In ParameterText.Split(","c)
                Dim ParameterName = Part.Trim()
                If Not PsText.IsValidIdentifier(ParameterName) Then
                    Throw New PsScriptException($"参数名「{ParameterName}」不合法。", Opening.Number)
                End If
                Parameters.Add(ParameterName)
            Next
        End If

        Dim Tail = Rest.Substring(CloseIndex + 1).Trim()
        Dim HasBrace = StripTrailingBrace(Tail)
        If Tail.Length > 0 AndAlso Tail <> ":" Then
            Throw New PsScriptException($"函数声明末尾存在无法识别的内容「{Tail}」。", Opening.Number)
        End If

        _index += 1
        Dim Body As List(Of PsStmt)
        If HasBrace Then
            Body = ParseBraceBlock(Opening.Number)
        Else
            Body = ParseIndentedBlock(Opening.Indent)
        End If

        Return New PsFunctionStmt With {
            .Line = Opening.Number,
            .Name = Name,
            .Parameters = Parameters,
            .Body = Body
        }
    End Function

    Private Function ParseIf(Opening As PsSourceLine) As PsStmt
        Dim ConditionText = Opening.Text.Substring("if".Length).Trim()
        Dim HasBrace = StripTrailingBrace(ConditionText)
        If ConditionText.Length = 0 Then
            Throw New PsScriptException("if 后缺少条件。", Opening.Number)
        End If

        Dim Condition = ParseExpression(ConditionText, Opening.Number)
        _index += 1

        Dim ThenBody As List(Of PsStmt)
        If HasBrace Then
            ThenBody = ParseBraceBlock(Opening.Number)
        Else
            ThenBody = ParseIndentedBlock(Opening.Indent)
        End If

        Dim ElseBody As New List(Of PsStmt)()
        Dim FoundElse = False

        If HasBrace AndAlso _braceRemainder.Length > 0 Then
            Dim Remainder = _braceRemainder
            If StartsWord(Remainder, "else") Then
                Dim Tail = Remainder.Substring("else".Length).Trim()
                ElseBody = ParseElseTail(Opening.Indent, Tail, Opening.Number)
                FoundElse = True
            Else
                Throw New PsScriptException($"右大括号后存在无法识别的内容「{Remainder}」。", Opening.Number)
            End If
        End If

        If Not FoundElse AndAlso _index < _lines.Count Then
            Dim NextLine = _lines(_index)
            If StartsWord(NextLine.Text, "else") AndAlso (HasBrace OrElse NextLine.Indent = Opening.Indent) Then
                Dim Tail = NextLine.Text.Substring("else".Length).Trim()
                _index += 1
                ElseBody = ParseElseTail(Opening.Indent, Tail, NextLine.Number)
            End If
        End If

        Return New PsIfStmt With {
            .Line = Opening.Number,
            .Condition = Condition,
            .ThenBody = ThenBody,
            .ElseBody = ElseBody
        }
    End Function

    Private Function ParseElseTail(LineIndent As Integer, Tail As String, LineNumber As Integer) As List(Of PsStmt)
        Dim HasBrace = StripTrailingBrace(Tail)
        If HasBrace Then Return ParseBraceBlock(LineNumber)
        If Tail.Length = 0 OrElse Tail = ":" Then Return ParseIndentedBlock(LineIndent)
        Throw New PsScriptException($"暂不支持该 else 写法：「{Tail}」。", LineNumber)
    End Function

    Private Function ParseWhile(Opening As PsSourceLine) As PsStmt
        Dim ConditionText = Opening.Text.Substring("while".Length).Trim()
        Dim HasBrace = StripTrailingBrace(ConditionText)
        If ConditionText.Length = 0 Then
            Throw New PsScriptException("while 后缺少条件。", Opening.Number)
        End If

        Dim Condition = ParseExpression(ConditionText, Opening.Number)
        _index += 1

        Dim Body As List(Of PsStmt)
        If HasBrace Then
            Body = ParseBraceBlock(Opening.Number)
        Else
            Body = ParseIndentedBlock(Opening.Indent)
        End If

        Return New PsWhileStmt With {
            .Line = Opening.Number,
            .Condition = Condition,
            .Body = Body
        }
    End Function

    Private Function ParseReturn(Opening As PsSourceLine) As PsStmt
        Dim ValueText = Opening.Text.Substring("return".Length).Trim()
        Dim Value As PsExpr = Nothing
        If ValueText.Length > 0 Then Value = ParseExpression(ValueText, Opening.Number)
        _index = Math.Max(_index + 1, _afterSimpleStatement)
        Return New PsReturnStmt With {.Line = Opening.Number, .Value = Value}
    End Function

    Private Function ParseLet(Opening As PsSourceLine) As PsStmt
        Dim IsConstant = StartsWord(Opening.Text, "const")
        Dim KeywordLength = If(IsConstant, "const".Length, "let".Length)
        Dim Rest = Opening.Text.Substring(KeywordLength).Trim()
        Dim EqualsIndex = PsText.FindAssignmentEquals(Rest)
        If EqualsIndex < 0 Then
            Throw New PsScriptException("变量声明缺少等号「=」。", Opening.Number)
        End If

        Dim Name = Rest.Substring(0, EqualsIndex).Trim()
        If Not PsText.IsValidIdentifier(Name) Then
            Throw New PsScriptException($"变量名「{Name}」不合法。", Opening.Number)
        End If

        Dim ValueText = Rest.Substring(EqualsIndex + 1).Trim()
        If ValueText.Length = 0 Then
            Throw New PsScriptException("变量声明缺少初始值。", Opening.Number)
        End If

        Dim Value = ParseExpression(ValueText, Opening.Number)
        _index = Math.Max(_index + 1, _afterSimpleStatement)
        Return New PsLetStmt With {
            .Line = Opening.Number,
            .Name = Name,
            .Value = Value,
            .IsConstant = IsConstant
        }
    End Function

    Private Function ParseSimple(Opening As PsSourceLine) As PsStmt
        Dim Text = Opening.Text
        Dim EqualsIndex = PsText.FindAssignmentEquals(Text)
        Dim Result As PsStmt

        If EqualsIndex >= 0 Then
            Dim TargetText = Text.Substring(0, EqualsIndex).Trim()
            Dim ValueText = Text.Substring(EqualsIndex + 1).Trim()
            If TargetText.Length = 0 Then
                Throw New PsScriptException("赋值语句缺少目标。", Opening.Number)
            End If
            If ValueText.Length = 0 Then
                Throw New PsScriptException("赋值语句缺少值。", Opening.Number)
            End If

            Dim Target As New PsTarget()
            For Each Part In TargetText.Split("."c)
                Dim Segment = Part.Trim()
                If Not PsText.IsValidIdentifier(Segment) Then
                    Throw New PsScriptException($"赋值目标「{TargetText}」不合法。", Opening.Number)
                End If
                Target.Segments.Add(Segment)
            Next

            Result = New PsAssignStmt With {
                .Line = Opening.Number,
                .Target = Target,
                .Value = ParseExpression(ValueText, Opening.Number)
            }
        Else
            Result = New PsExprStmt With {
                .Line = Opening.Number,
                .Expression = ParseExpression(Text, Opening.Number)
            }
        End If

        _index = Math.Max(_index + 1, _afterSimpleStatement)
        Return Result
    End Function

    Private Function ParseBraceBlock(OpeningLine As Integer) As List(Of PsStmt)
        Dim Result As New List(Of PsStmt)()
        While _index < _lines.Count
            Dim CurrentLine = _lines(_index)
            If CurrentLine.Text.StartsWith("}") Then
                _index += 1
                _braceRemainder = PsText.StripTrailingSemicolon(CurrentLine.Text.Substring(1).Trim())
                Return Result
            End If
            If StartsWord(CurrentLine.Text, "else") Then
                Throw New PsScriptException("「else」没有对应的「if」。", CurrentLine.Number)
            End If
            Result.Add(ParseStatement())
        End While
        Throw New PsScriptException("代码块缺少右大括号「}」。", OpeningLine)
    End Function

    Private Function ParseIndentedBlock(ParentIndent As Integer) As List(Of PsStmt)
        Dim Result As New List(Of PsStmt)()
        If _index >= _lines.Count Then Return Result

        Dim BaseIndent = _lines(_index).Indent
        If BaseIndent <= ParentIndent Then Return Result

        While _index < _lines.Count AndAlso _lines(_index).Indent >= BaseIndent
            Result.Add(ParseStatement())
        End While
        Return Result
    End Function

    Private Function ParseExpression(Text As String, Line As Integer) As PsExpr
        Return New PsExpressionParser(Text, Line, _islandValues).Parse()
    End Function

    Private Shared Function StripTrailingBrace(ByRef Text As String) As Boolean
        Dim Trimmed = Text.TrimEnd()
        If Trimmed.EndsWith("{") Then
            Text = Trimmed.Substring(0, Trimmed.Length - 1).TrimEnd()
            Return True
        End If
        Return False
    End Function

    '#Region "island 解析"

    Private Function ParseIslandBody(Lines As List(Of PsSourceLine), OpeningLine As Integer) As Object
        If Lines Is Nothing OrElse Lines.Count = 0 Then Return PsValues.NewMap()
        Dim Index = 0
        Return ParseIslandContainer(Lines, Index, Lines(0).Indent, OpeningLine)
    End Function

    Private Function ParseIslandContainer(Lines As List(Of PsSourceLine), ByRef Index As Integer, BaseIndent As Integer, OpeningLine As Integer) As Object
        If Index >= Lines.Count Then Return PsValues.NewMap()

        If IsDashEntry(Lines(Index).Text) Then
            Dim Result As New List(Of Object)()
            While Index < Lines.Count AndAlso Lines(Index).Indent >= BaseIndent
                Dim CurrentLine = Lines(Index)
                If CurrentLine.Indent > BaseIndent Then
                    Throw New PsScriptException("island 中出现了意外的缩进。", CurrentLine.Number)
                End If
                If Not IsDashEntry(CurrentLine.Text) Then
                    Throw New PsScriptException("island 列表项中不能混用普通属性。", CurrentLine.Number)
                End If

                Dim Item = PsValues.NewMap()
                Dim EntryText = CurrentLine.Text.Substring(1).Trim()
                If EntryText.Length = 0 Then
                    Throw New PsScriptException("island 列表项「-」后缺少内容。", CurrentLine.Number)
                End If

                ParseIslandEntryText(EntryText, Lines, Index, BaseIndent, Item, CurrentLine)

                If Index < Lines.Count AndAlso Lines(Index).Indent > BaseIndent Then
                    Dim ChildBase = Lines(Index).Indent
                    Dim Extra = ParseIslandContainer(Lines, Index, ChildBase, CurrentLine.Number)
                    If Not PsValues.DeepMerge(Item, Extra) Then
                        Throw New PsScriptException("island 列表项的附加属性必须是对象。", CurrentLine.Number)
                    End If
                End If

                Result.Add(Item)
            End While
            Return Result
        Else
            Dim Result = PsValues.NewMap()
            While Index < Lines.Count AndAlso Lines(Index).Indent >= BaseIndent
                Dim CurrentLine = Lines(Index)
                If CurrentLine.Indent > BaseIndent Then
                    Throw New PsScriptException("island 中出现了意外的缩进。", CurrentLine.Number)
                End If
                If IsDashEntry(CurrentLine.Text) Then
                    Throw New PsScriptException("island 对象中不能混用「-」列表项。", CurrentLine.Number)
                End If
                ParseIslandEntryText(CurrentLine.Text, Lines, Index, BaseIndent, Result, CurrentLine)
            End While
            Return Result
        End If
    End Function

    Private Sub ParseIslandEntryText(EntryText As String, Lines As List(Of PsSourceLine), ByRef Index As Integer, BaseIndent As Integer, Target As IDictionary, EntryLine As PsSourceLine)
        Dim ColonIndex = PsText.FindTopLevelChar(EntryText, ":"c)
        If ColonIndex < 0 Then
            Throw New PsScriptException($"island 条目「{EntryText}」缺少冒号。", EntryLine.Number)
        End If

        Dim Key = EntryText.Substring(0, ColonIndex).Trim()
        Dim ValueText = EntryText.Substring(ColonIndex + 1).Trim()
        If Key.Length = 0 Then
            Throw New PsScriptException("island 条目的键不能为空。", EntryLine.Number)
        End If

        If ValueText.Length > 0 Then
            Dim NextIndex = Index
            Dim Value = ParseIslandInlineValue(ValueText, Lines, NextIndex, EntryLine)
            If NextIndex <= Index Then NextIndex = Index + 1
            Index = NextIndex
            Target(Key) = Value
            Return
        End If

        Index += 1
        If Index < Lines.Count AndAlso Lines(Index).Indent > BaseIndent Then
            Dim ChildBase = Lines(Index).Indent
            If IsBareScalarSeries(Lines, Index, ChildBase) Then
                Target(Key) = ParseScalarSeries(Lines, Index, ChildBase)
            Else
                Target(Key) = ParseIslandContainer(Lines, Index, ChildBase, EntryLine.Number)
            End If
        Else
            Target(Key) = Nothing
        End If
    End Sub

    Private Function ParseIslandInlineValue(ValueText As String, Lines As List(Of PsSourceLine), ByRef Index As Integer, EntryLine As PsSourceLine) As Object
        Dim Trimmed = ValueText.Trim()

        If StartsWord(Trimmed, "island") Then
            Dim After = Trimmed.Substring("island".Length).Trim()
            If After.StartsWith("{") Then
                Dim Capture = CaptureNestedBraceIsland(Lines, Index, EntryLine.Number)
                Index = Capture.NextIndex
                Return ParseIslandBody(Capture.Lines, EntryLine.Number)
            ElseIf After.Length = 0 Then
                If Index + 1 < Lines.Count AndAlso Lines(Index + 1).Text.Trim() = "{" Then
                    Dim Capture = CaptureNestedBraceIsland(Lines, Index + 1, EntryLine.Number)
                    Index = Capture.NextIndex
                    Return ParseIslandBody(Capture.Lines, EntryLine.Number)
                Else
                    Dim Body As New List(Of PsSourceLine)()
                    Dim NextLineIndex = Index + 1
                    While NextLineIndex < Lines.Count AndAlso Lines(NextLineIndex).Indent > Lines(Index).Indent
                        Body.Add(Lines(NextLineIndex))
                        NextLineIndex += 1
                    End While
                    Index = NextLineIndex
                    Return ParseIslandBody(Body, EntryLine.Number)
                End If
            End If
        End If

        Dim Parts = PsText.SplitTopLevel(Trimmed, ","c)
        If Parts.Count > 1 Then
            Dim Result As New List(Of Object)()
            For Each Part In Parts
                If Part.Trim().Length > 0 Then Result.Add(ParseIslandScalar(Part, EntryLine.Number))
            Next
            Return Result
        End If

        Return ParseIslandScalar(Trimmed, EntryLine.Number)
    End Function

    Private Function CaptureNestedBraceIsland(Lines As List(Of PsSourceLine), OpeningIndex As Integer, OpeningLine As Integer) As PsIslandCapture
        Dim Capture As New PsIslandCapture()
        Dim Depth = 1
        Dim Index = OpeningIndex + 1
        While Index < Lines.Count
            Dim CurrentLine = Lines(Index)
            Dim Delta = PsText.CountBraceDelta(CurrentLine.Text)
            If Depth + Delta <= 0 Then
                Capture.NextIndex = Index + 1
                Return Capture
            End If
            Capture.Lines.Add(CurrentLine)
            Depth += Delta
            Index += 1
        End While
        Throw New PsScriptException("嵌套 island 缺少右大括号「}」。", OpeningLine)
    End Function

    Private Function ParseIslandScalar(Text As String, Line As Integer) As Object
        Dim Trimmed = Text.Trim()
        If Trimmed.Length = 0 Then Return ""

        If Trimmed.StartsWith("""") OrElse Trimmed.StartsWith("'") Then
            Dim Tokens = New PsLexer(Trimmed, Line).Tokenize()
            If Tokens.Count < 2 OrElse Tokens(0).Kind <> PsTokenKind.StringLiteral Then
                Throw New PsScriptException($"island 中的字符串「{Trimmed}」格式不正确。", Line)
            End If
            Return Tokens(0).Value
        End If

        If Trimmed.StartsWith("[") AndAlso Trimmed.EndsWith("]") Then
            Dim Inner = Trimmed.Substring(1, Trimmed.Length - 2)
            Dim Result As New List(Of Object)()
            For Each Part In PsText.SplitTopLevel(Inner, ","c)
                If Part.Trim().Length > 0 Then Result.Add(ParseIslandScalar(Part, Line))
            Next
            Return Result
        End If

        Dim AsLong As Long
        If Long.TryParse(Trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, AsLong) Then Return AsLong

        Dim AsDouble As Double
        If Double.TryParse(Trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, AsDouble) Then Return AsDouble

        If String.Equals(Trimmed, "true", StringComparison.OrdinalIgnoreCase) Then Return True
        If String.Equals(Trimmed, "false", StringComparison.OrdinalIgnoreCase) Then Return False
        If String.Equals(Trimmed, "nothing", StringComparison.OrdinalIgnoreCase) OrElse
           String.Equals(Trimmed, "null", StringComparison.OrdinalIgnoreCase) Then Return Nothing

        Return Trimmed
    End Function

    Private Function IsBareScalarSeries(Lines As List(Of PsSourceLine), StartIndex As Integer, BaseIndent As Integer) As Boolean
        Dim Index = StartIndex
        While Index < Lines.Count AndAlso Lines(Index).Indent >= BaseIndent
            If Lines(Index).Indent = BaseIndent Then
                Dim Text = Lines(Index).Text
                If IsDashEntry(Text) OrElse PsText.FindTopLevelChar(Text, ":"c) >= 0 Then Return False
            End If
            Index += 1
        End While
        Return True
    End Function

    Private Function ParseScalarSeries(Lines As List(Of PsSourceLine), ByRef Index As Integer, BaseIndent As Integer) As List(Of Object)
        Dim Result As New List(Of Object)()
        While Index < Lines.Count AndAlso Lines(Index).Indent >= BaseIndent
            Dim CurrentLine = Lines(Index)
            If CurrentLine.Indent > BaseIndent Then
                Throw New PsScriptException("island 的标量列表出现了意外缩进。", CurrentLine.Number)
            End If
            For Each Part In PsText.SplitTopLevel(CurrentLine.Text, ","c)
                If Part.Trim().Length > 0 Then Result.Add(ParseIslandScalar(Part, CurrentLine.Number))
            Next
            Index += 1
        End While
        Return Result
    End Function

    Private Shared Function IsDashEntry(Text As String) As Boolean
        If Text Is Nothing OrElse Not Text.StartsWith("-") Then Return False
        If Text.Length = 1 Then Return True
        Dim NextCharacter = Text(1)
        ' 负数字面量不是列表项。
        Return Not Char.IsDigit(NextCharacter) AndAlso NextCharacter <> "."c
    End Function

    '#End Region

End Class
