' PlugScript 对外公开的稳定 API。
Option Strict Off

''' <summary>PlugScript 解释器入口。</summary>
Public Module PlugScriptEngine

    ''' <summary>
    ''' 解析并执行顶层语句，然后可选地调用指定函数。
    ''' 所有异常都会转换为 PlugScriptResult，不会向外抛出。
    ''' </summary>
    Public Function Execute(Source As String, Context As PlugScriptContext, Optional EntryPoint As String = Nothing) As PlugScriptResult
        If Context Is Nothing Then Context = New PlugScriptContext()
        Dim Result As New PlugScriptResult With {.Context = Context}

        Try
            Dim Program = New PsParser().Parse(Source)
            Dim Runner As New PsInterpreter(Program, Context)
            Runner.RunTopLevel()

            If Not String.IsNullOrWhiteSpace(EntryPoint) Then
                Result.Result = Runner.InvokeFunction(EntryPoint.Trim(), New Object() {}, 0)
            End If

            Result.Success = True
        Catch ScriptError As PsScriptException
            Result.Success = False
            Result.ErrorLine = ScriptError.Line
            Result.ErrorMessage = FormatError(ScriptError.Message, ScriptError.Line)
        Catch Ex As Exception
            Result.Success = False
            Result.ErrorLine = 0
            Result.ErrorMessage = Ex.Message
        End Try

        Return Result
    End Function

    ''' <summary>
    ''' 解析源码中的函数声明，然后只调用指定函数，不执行顶层语句。
    ''' </summary>
    Public Function ExecuteFunction(Source As String, Context As PlugScriptContext, FunctionName As String) As PlugScriptResult
        If Context Is Nothing Then Context = New PlugScriptContext()
        Dim Result As New PlugScriptResult With {.Context = Context}

        If String.IsNullOrWhiteSpace(FunctionName) Then
            Result.Success = False
            Result.ErrorLine = 0
            Result.ErrorMessage = "函数名不能为空。"
            Return Result
        End If

        Try
            Dim Program = New PsParser().Parse(Source)
            Dim Runner As New PsInterpreter(Program, Context)
            Result.Result = Runner.InvokeFunction(FunctionName.Trim(), New Object() {}, 0)
            Result.Success = True
        Catch ScriptError As PsScriptException
            Result.Success = False
            Result.ErrorLine = ScriptError.Line
            Result.ErrorMessage = FormatError(ScriptError.Message, ScriptError.Line)
        Catch Ex As Exception
            Result.Success = False
            Result.ErrorLine = 0
            Result.ErrorMessage = Ex.Message
        End Try

        Return Result
    End Function

    ''' <summary>
    ''' 运行内置自检，返回可直接显示的多行报告。
    ''' </summary>
    Public Function SelfTest() As String
        Try
            Return PsSelfTest.Run()
        Catch Ex As Exception
            Return "PlugScript 自检" & vbCrLf & "[FAIL] 自检自身异常：" & Ex.GetType().Name & ": " & Ex.Message
        End Try
    End Function

    Private Function FormatError(Message As String, Line As Integer) As String
        If Line > 0 Then Return $"第 {Line} 行：{Message}"
        Return Message
    End Function

End Module
