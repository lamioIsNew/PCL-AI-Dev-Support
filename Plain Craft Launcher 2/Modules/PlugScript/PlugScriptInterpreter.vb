' PlugScript 的求值器。负责作用域、赋值、控制流与内置函数。
Option Strict Off

Imports System.Collections
Imports System.Collections.Generic
Imports System.Globalization
Imports IDictionary = System.Collections.IDictionary
Imports IList = System.Collections.IList

''' <summary>函数 return 使用异常实现，便于从任意嵌套语句直接返回。</summary>
Friend Class PsReturnSignal
    Inherits Exception
    Public ReadOnly Property Value As Object

    Public Sub New(Value As Object)
        MyBase.New("return")
        Me.Value = Value
    End Sub
End Class

''' <summary>词法作用域。全局作用域直接使用 PlugScriptContext.Variables。</summary>
Friend Class PsScope
    Public ReadOnly Property Parent As PsScope
    Public ReadOnly Property Variables As Dictionary(Of String, Object)
    Public ReadOnly Property Constants As New HashSet(Of String)(StringComparer.OrdinalIgnoreCase)

    Public Sub New(Parent As PsScope, Variables As Dictionary(Of String, Object))
        Me.Parent = Parent
        Me.Variables = Variables
    End Sub

    Public Function Find(Name As String) As PsScope
        Dim Current As PsScope = Me
        While Current IsNot Nothing
            If Current.Variables.ContainsKey(Name) Then Return Current
            Current = Current.Parent
        End While
        Return Nothing
    End Function

    Public Function TryGet(Name As String, ByRef Value As Object) As Boolean
        Dim Owner = Find(Name)
        If Owner Is Nothing Then Return False
        Value = Owner.Variables(Name)
        Return True
    End Function

    Public Sub Define(Name As String, Value As Object, IsConstant As Boolean, Line As Integer)
        If Constants.Contains(Name) Then
            Throw New PsScriptException($"常量「{Name}」不能重复声明。", Line)
        End If
        Variables(Name) = Value
        If IsConstant Then Constants.Add(Name)
    End Sub

    Public Sub Assign(Name As String, Value As Object, Line As Integer)
        Dim Owner = Find(Name)
        If Owner Is Nothing Then
            Variables(Name) = Value
            Return
        End If
        If Owner.Constants.Contains(Name) Then
            Throw New PsScriptException($"常量「{Name}」不能被重新赋值。", Line)
        End If
        Owner.Variables(Name) = Value
    End Sub
End Class

''' <summary>执行 PsProgram。</summary>
Friend Class PsInterpreter

    Private Const MaxLoopIterations As Integer = 1000000
    Private Const MaxCallDepth As Integer = 128

    Private ReadOnly _context As PlugScriptContext
    Private ReadOnly _program As PsProgram
    Private ReadOnly _global As PsScope
    Private ReadOnly _builtins As Dictionary(Of String, Object)
    Private _functionDepth As Integer

    Public Sub New(Program As PsProgram, Context As PlugScriptContext)
        _program = Program
        _context = Context
        _global = New PsScope(Nothing, Context.Variables)
        _builtins = BuildBuiltins()
    End Sub

    Public Function RunTopLevel() As Object
        ExecuteStatements(_program.Statements, _global)
        Return Nothing
    End Function

    Public Function InvokeFunction(Name As String, Arguments As Object(), CallLine As Integer) As Object
        Dim FunctionStatement As PsFunctionStmt = Nothing
        If Not _program.Functions.TryGetValue(Name, FunctionStatement) Then
            Throw New PsScriptException($"未定义的函数「{Name}」。", CallLine)
        End If

        If Arguments.Length <> FunctionStatement.Parameters.Count Then
            Throw New PsScriptException(
                $"函数「{Name}」需要 {FunctionStatement.Parameters.Count} 个参数，实际传入 {Arguments.Length} 个。",
                CallLine)
        End If

        If _functionDepth >= MaxCallDepth Then
            Throw New PsScriptException($"函数调用层数超过上限（{MaxCallDepth}），可能存在无限递归。", CallLine)
        End If

        Dim LocalVariables As New Dictionary(Of String, Object)(StringComparer.OrdinalIgnoreCase)
        Dim LocalScope As New PsScope(_global, LocalVariables)
        For Index = 0 To FunctionStatement.Parameters.Count - 1
            LocalVariables(FunctionStatement.Parameters(Index)) = Arguments(Index)
        Next

        _functionDepth += 1
        Try
            ExecuteStatements(FunctionStatement.Body, LocalScope)
            Return Nothing
        Catch ReturnSignal As PsReturnSignal
            Return ReturnSignal.Value
        Finally
            _functionDepth -= 1
        End Try
    End Function

    Private Sub ExecuteStatements(Statements As List(Of PsStmt), Scope As PsScope)
        For Each Statement In Statements
            ExecuteStatement(Statement, Scope)
        Next
    End Sub

    Private Sub ExecuteStatement(Statement As PsStmt, Scope As PsScope)
        If TypeOf Statement Is PsLetStmt Then
            Dim LetStatement = DirectCast(Statement, PsLetStmt)
            Dim Value = Evaluate(LetStatement.Value, Scope)
            Scope.Define(LetStatement.Name, Value, LetStatement.IsConstant, LetStatement.Line)
            Return
        End If

        If TypeOf Statement Is PsAssignStmt Then
            Dim AssignStatement = DirectCast(Statement, PsAssignStmt)
            Dim Value = Evaluate(AssignStatement.Value, Scope)
            AssignTarget(AssignStatement.Target, Value, Scope, AssignStatement.Line)
            Return
        End If

        If TypeOf Statement Is PsExprStmt Then
            Evaluate(DirectCast(Statement, PsExprStmt).Expression, Scope)
            Return
        End If

        If TypeOf Statement Is PsIfStmt Then
            Dim IfStatement = DirectCast(Statement, PsIfStmt)
            If PsValues.IsTruthy(Evaluate(IfStatement.Condition, Scope)) Then
                ExecuteStatements(IfStatement.ThenBody, Scope)
            Else
                ExecuteStatements(IfStatement.ElseBody, Scope)
            End If
            Return
        End If

        If TypeOf Statement Is PsWhileStmt Then
            Dim WhileStatement = DirectCast(Statement, PsWhileStmt)
            Dim Iterations = 0
            While PsValues.IsTruthy(Evaluate(WhileStatement.Condition, Scope))
                Iterations += 1
                If Iterations > MaxLoopIterations Then
                    Throw New PsScriptException($"while 循环超过 {MaxLoopIterations} 次，已中止。", WhileStatement.Line)
                End If
                ExecuteStatements(WhileStatement.Body, Scope)
            End While
            Return
        End If

        If TypeOf Statement Is PsFunctionStmt Then
            ' 顶层函数已在解析后注册；嵌套函数声明不执行任何操作。
            Return
        End If

        If TypeOf Statement Is PsReturnStmt Then
            Dim ReturnStatement = DirectCast(Statement, PsReturnStmt)
            If _functionDepth <= 0 Then
                Throw New PsScriptException("return 只能在函数内部使用。", ReturnStatement.Line)
            End If
            Dim Value As Object = Nothing
            If ReturnStatement.Value IsNot Nothing Then Value = Evaluate(ReturnStatement.Value, Scope)
            Throw New PsReturnSignal(Value)
        End If

        Throw New PsScriptException("无法识别的语句。", Statement.Line)
    End Sub

    Private Sub AssignTarget(Target As PsTarget, Value As Object, Scope As PsScope, Line As Integer)
        If Target.Segments.Count = 0 Then Throw New PsScriptException("赋值目标为空。", Line)

        If Target.Segments.Count = 1 Then
            Scope.Assign(Target.Segments(0), Value, Line)
            Return
        End If

        Dim Current = ResolveIdentifier(Target.Segments(0), Scope, Line)
        For Index = 1 To Target.Segments.Count - 2
            Dim Segment = Target.Segments(Index)
            Dim Child As Object = Nothing
            If PsValues.TryGetMember(Current, Segment, Child) Then
                Current = Child
            ElseIf TypeOf Current Is IDictionary Then
                Child = PsValues.NewMap()
                DirectCast(Current, IDictionary)(Segment) = Child
                Current = Child
            Else
                Throw New PsScriptException($"无法访问「{Target.Segments(0)}」的成员「{Segment}」。", Line)
            End If
        Next

        PsValues.SetMember(Current, Target.Segments(Target.Segments.Count - 1), Value, Line)
    End Sub

    Private Function Evaluate(Expression As PsExpr, Scope As PsScope) As Object
        If Expression Is Nothing Then Return Nothing

        If TypeOf Expression Is PsLiteralExpr Then Return DirectCast(Expression, PsLiteralExpr).Value
        If TypeOf Expression Is PsIdentifierExpr Then
            Return ResolveIdentifier(DirectCast(Expression, PsIdentifierExpr).Name, Scope, Expression.Line)
        End If
        If TypeOf Expression Is PsMemberExpr Then
            Dim MemberExpression = DirectCast(Expression, PsMemberExpr)
            Dim Target = Evaluate(MemberExpression.Target, Scope)
            Dim Value As Object = Nothing
            If PsValues.TryGetMember(Target, MemberExpression.MemberName, Value) Then Return Value
            If TypeOf Target Is IDictionary Then Return Nothing
            Throw New PsScriptException($"无法读取成员「{MemberExpression.MemberName}」。", Expression.Line)
        End If
        If TypeOf Expression Is PsIndexExpr Then
            Dim IndexExpression = DirectCast(Expression, PsIndexExpr)
            Dim Target = Evaluate(IndexExpression.Target, Scope)
            Dim IndexValue = Evaluate(IndexExpression.Index, Scope)
            Return PsValues.GetIndex(Target, IndexValue, Expression.Line)
        End If
        If TypeOf Expression Is PsArrayExpr Then
            Dim ArrayExpression = DirectCast(Expression, PsArrayExpr)
            Dim Result As New List(Of Object)()
            For Each Item In ArrayExpression.Items
                Result.Add(Evaluate(Item, Scope))
            Next
            Return Result
        End If
        If TypeOf Expression Is PsUnaryExpr Then
            Return EvaluateUnary(DirectCast(Expression, PsUnaryExpr), Scope)
        End If
        If TypeOf Expression Is PsBinaryExpr Then
            Return EvaluateBinary(DirectCast(Expression, PsBinaryExpr), Scope)
        End If
        If TypeOf Expression Is PsCallExpr Then
            Return EvaluateCall(DirectCast(Expression, PsCallExpr), Scope)
        End If

        Throw New PsScriptException("无法识别的表达式。", Expression.Line)
    End Function

    Private Function ResolveIdentifier(Name As String, Scope As PsScope, Line As Integer) As Object
        Dim Value As Object = Nothing
        If Scope.TryGet(Name, Value) Then Return Value
        If _builtins.ContainsKey(Name) Then Return _builtins(Name)

        Dim FunctionStatement As PsFunctionStmt = Nothing
        If _program.Functions.TryGetValue(Name, FunctionStatement) Then Return New PsUserFunction(FunctionStatement.Name)

        Throw New PsScriptException($"未定义的标识符「{Name}」。", Line)
    End Function

    Private Function EvaluateUnary(Expression As PsUnaryExpr, Scope As PsScope) As Object
        Dim Value = Evaluate(Expression.Operand, Scope)
        Select Case Expression.Operator.ToLowerInvariant()
            Case "-"
                Return -PsValues.ToNumber(Value, Expression.Line)
            Case "+"
                Return PsValues.ToNumber(Value, Expression.Line)
            Case "!", "not"
                Return Not PsValues.IsTruthy(Value)
            Case Else
                Throw New PsScriptException($"不支持的一元运算符「{Expression.Operator}」。", Expression.Line)
        End Select
    End Function

    Private Function EvaluateBinary(Expression As PsBinaryExpr, Scope As PsScope) As Object
        Dim OperatorText = Expression.Operator

        If OperatorText = "&&" Then
            If Not PsValues.IsTruthy(Evaluate(Expression.Left, Scope)) Then Return False
            Return PsValues.IsTruthy(Evaluate(Expression.Right, Scope))
        End If
        If OperatorText = "||" Then
            If PsValues.IsTruthy(Evaluate(Expression.Left, Scope)) Then Return True
            Return PsValues.IsTruthy(Evaluate(Expression.Right, Scope))
        End If

        Dim Left = Evaluate(Expression.Left, Scope)
        Dim Right = Evaluate(Expression.Right, Scope)

        Select Case OperatorText
            Case "+"
                If TypeOf Left Is String OrElse TypeOf Right Is String Then
                    Return PsValues.ToText(Left) & PsValues.ToText(Right)
                End If
                If IsIntegral(Left) AndAlso IsIntegral(Right) Then
                    Return Convert.ToInt64(Left, CultureInfo.InvariantCulture) + Convert.ToInt64(Right, CultureInfo.InvariantCulture)
                End If
                Return PsValues.ToNumber(Left, Expression.Line) + PsValues.ToNumber(Right, Expression.Line)

            Case "-"
                If IsIntegral(Left) AndAlso IsIntegral(Right) Then
                    Return Convert.ToInt64(Left, CultureInfo.InvariantCulture) - Convert.ToInt64(Right, CultureInfo.InvariantCulture)
                End If
                Return PsValues.ToNumber(Left, Expression.Line) - PsValues.ToNumber(Right, Expression.Line)

            Case "*"
                If IsIntegral(Left) AndAlso IsIntegral(Right) Then
                    Return Convert.ToInt64(Left, CultureInfo.InvariantCulture) * Convert.ToInt64(Right, CultureInfo.InvariantCulture)
                End If
                Return PsValues.ToNumber(Left, Expression.Line) * PsValues.ToNumber(Right, Expression.Line)

            Case "/"
                Dim Divisor = PsValues.ToNumber(Right, Expression.Line)
                If Divisor = 0.0R Then Throw New PsScriptException("除数不能为零。", Expression.Line)
                Return PsValues.ToNumber(Left, Expression.Line) / Divisor

            Case "%"
                Dim Divisor = PsValues.ToNumber(Right, Expression.Line)
                If Divisor = 0.0R Then Throw New PsScriptException("取余的除数不能为零。", Expression.Line)
                If IsIntegral(Left) AndAlso IsIntegral(Right) Then
                    Return Convert.ToInt64(Left, CultureInfo.InvariantCulture) Mod Convert.ToInt64(Right, CultureInfo.InvariantCulture)
                End If
                Return PsValues.ToNumber(Left, Expression.Line) Mod Divisor

            Case "=="
                Return PsValues.ValueEquals(Left, Right)

            Case "!="
                Return Not PsValues.ValueEquals(Left, Right)

            Case ">", "<", ">=", "<="
                Dim Comparison As Integer
                If PsValues.IsNumeric(Left) AndAlso PsValues.IsNumeric(Right) Then
                    Comparison = PsValues.ToNumber(Left, Expression.Line).CompareTo(PsValues.ToNumber(Right, Expression.Line))
                ElseIf TypeOf Left Is String AndAlso TypeOf Right Is String Then
                    Comparison = String.CompareOrdinal(CStr(Left), CStr(Right))
                Else
                    Throw New PsScriptException("比较运算需要两个数字或两个字符串。", Expression.Line)
                End If
                Select Case OperatorText
                    Case ">" : Return Comparison > 0
                    Case "<" : Return Comparison < 0
                    Case ">=" : Return Comparison >= 0
                    Case "<=" : Return Comparison <= 0
                End Select

        End Select

        Throw New PsScriptException($"不支持的运算符「{OperatorText}」。", Expression.Line)
    End Function

    Private Function EvaluateCall(Expression As PsCallExpr, Scope As PsScope) As Object
        Dim Callee = Evaluate(Expression.Callee, Scope)
        Dim Arguments As New List(Of Object)()
        For Each ArgumentExpression In Expression.Arguments
            Arguments.Add(Evaluate(ArgumentExpression, Scope))
        Next

        Dim NativeFunction = TryCast(Callee, PsNativeFunction)
        If NativeFunction IsNot Nothing Then
            Try
                Return NativeFunction.Handler(Arguments.ToArray())
            Catch ScriptError As PsScriptException
                If ScriptError.Line <= 0 Then Throw New PsScriptException(ScriptError.Message, Expression.Line)
                Throw
            Catch Other As Exception
                Throw New PsScriptException(Other.Message, Expression.Line)
            End Try
        End If

        Dim UserFunction = TryCast(Callee, PsUserFunction)
        If UserFunction IsNot Nothing Then
            Return InvokeFunction(UserFunction.Name, Arguments.ToArray(), Expression.Line)
        End If

        Throw New PsScriptException($"「{PsValues.ToText(Callee)}」不是可调用的函数。", Expression.Line)
    End Function

    Private Shared Function IsIntegral(Value As Object) As Boolean
        If Value Is Nothing Then Return False
        Return TypeOf Value Is Byte OrElse TypeOf Value Is SByte OrElse
               TypeOf Value Is Short OrElse TypeOf Value Is UShort OrElse
               TypeOf Value Is Integer OrElse TypeOf Value Is UInteger OrElse
               TypeOf Value Is Long OrElse TypeOf Value Is ULong
    End Function

    Private Function BuildBuiltins() As Dictionary(Of String, Object)
        Dim Result As New Dictionary(Of String, Object)(StringComparer.OrdinalIgnoreCase)

        Result("print") = New PsNativeFunction("print",
            Function(Arguments As Object()) As Object
                _context.Log(JoinArguments(Arguments))
                Return Nothing
            End Function)

        Result("log") = New PsNativeFunction("log",
            Function(Arguments As Object()) As Object
                _context.Log(JoinArguments(Arguments))
                Return Nothing
            End Function)

        Dim Storage = PsValues.NewMap()
        Storage("read") = New PsNativeFunction("Storage.read",
            Function(Arguments As Object()) As Object
                RequireArgumentCount("Storage.read", Arguments, 1)
                If _context.StorageRead Is Nothing Then Throw MissingDelegate("Storage.read")
                Return _context.StorageRead.Invoke(PsValues.ToText(Arguments(0)))
            End Function)
        Storage("write") = New PsNativeFunction("Storage.write",
            Function(Arguments As Object()) As Object
                RequireArgumentCount("Storage.write", Arguments, 2)
                If _context.StorageWrite Is Nothing Then Throw MissingDelegate("Storage.write")
                _context.StorageWrite.Invoke(PsValues.ToText(Arguments(0)), PsValues.ToText(Arguments(1)))
                Return Nothing
            End Function)
        Storage("exists") = New PsNativeFunction("Storage.exists",
            Function(Arguments As Object()) As Object
                RequireArgumentCount("Storage.exists", Arguments, 1)
                If _context.StorageExists Is Nothing Then Throw MissingDelegate("Storage.exists")
                Return _context.StorageExists.Invoke(PsValues.ToText(Arguments(0)))
            End Function)
        Storage("delete") = New PsNativeFunction("Storage.delete",
            Function(Arguments As Object()) As Object
                RequireArgumentCount("Storage.delete", Arguments, 1)
                If _context.StorageDelete Is Nothing Then Throw MissingDelegate("Storage.delete")
                _context.StorageDelete.Invoke(PsValues.ToText(Arguments(0)))
                Return Nothing
            End Function)
        Storage("list") = New PsNativeFunction("Storage.list",
            Function(Arguments As Object()) As Object
                RequireArgumentCount("Storage.list", Arguments, 0)
                If _context.StorageList Is Nothing Then Throw MissingDelegate("Storage.list")
                Return _context.StorageList.Invoke()
            End Function)
        Result("Storage") = Storage

        Dim Permission = PsValues.NewMap()
        Permission("has") = New PsNativeFunction("Permission.has",
            Function(Arguments As Object()) As Object
                RequireArgumentCount("Permission.has", Arguments, 1)
                Return _context.HasPermission(PsValues.ToText(Arguments(0)))
            End Function)
        Result("Permission") = Permission

        Result("Ui") = _context.Ui
        Result("Plugin") = _context.Plugin
        Return Result
    End Function

    Private Shared Function JoinArguments(Arguments As Object()) As String
        Dim Parts As New List(Of String)()
        For Each Argument In Arguments
            Parts.Add(PsValues.ToText(Argument))
        Next
        Return String.Join(" ", Parts)
    End Function

    Private Shared Sub RequireArgumentCount(Name As String, Arguments As Object(), Count As Integer)
        If Arguments.Length <> Count Then
            Throw New PsScriptException($"函数「{Name}」需要 {Count} 个参数，实际传入 {Arguments.Length} 个。", 0)
        End If
    End Sub

    Private Shared Function MissingDelegate(Name As String) As Exception
        Return New PsScriptException($"宿主未配置 {Name} 委托。", 0)
    End Function

End Class
