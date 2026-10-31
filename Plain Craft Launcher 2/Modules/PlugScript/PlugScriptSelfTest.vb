' PlugScript 内置自检，用于应用测试页与开发验证。
Option Strict Off

Imports System.Collections
Imports System.Collections.Generic
Imports System.Globalization
Imports IDictionary = System.Collections.IDictionary
Imports IList = System.Collections.IList

Friend Module PsSelfTest

    Public Function Run() As String
        Dim Report As New List(Of String)()
        Dim Passed As Integer = 0
        Dim Failed As Integer = 0

        Report.Add("PlugScript 自检")

        RunCase(Report, Passed, Failed, "变量、常量与赋值", AddressOf TestVariables)
        RunCase(Report, Passed, Failed, "island 嵌套列表与逗号值", AddressOf TestIsland)
        RunCase(Report, Passed, Failed, "纯缩进函数体", AddressOf TestIndentationFunction)
        RunCase(Report, Passed, Failed, "if / while / 算术 / 字符串拼接", AddressOf TestControlFlow)
        RunCase(Report, Passed, Failed, "错误行号报告", AddressOf TestErrorLine)
        RunCase(Report, Passed, Failed, "内置存储与权限委托", AddressOf TestBuiltins)
        RunCase(Report, Passed, Failed, "完整示例脚本", AddressOf TestFullExample)

        Report.Add("")
        Report.Add($"结果：{Passed} 项通过，{Failed} 项失败。")
        Return String.Join(vbCrLf, Report)
    End Function

    Private Sub RunCase(Report As List(Of String), ByRef Passed As Integer, ByRef Failed As Integer, Name As String, Test As Func(Of String))
        Try
            Dim Failure = Test()
            If Failure Is Nothing Then
                Report.Add($"[PASS] {Name}")
                Passed += 1
            Else
                Report.Add($"[FAIL] {Name}：{Failure}")
                Failed += 1
            End If
        Catch Ex As Exception
            Report.Add($"[FAIL] {Name}：{Ex.GetType().Name}: {Ex.Message}")
            Failed += 1
        End Try
    End Sub

    Private Function Lines(ParamArray Values As String()) As String
        Return String.Join(vbLf, Values)
    End Function

    Private Function TestVariables() As String
        Dim Context As New PlugScriptContext()
        Dim Source = Lines(
            "let player = ""steve""",
            "const play = True",
            "let score = 1",
            "score = score + 2")
        Dim Result = PlugScriptEngine.Execute(Source, Context)
        If Not Result.Success Then Return "执行失败：" & Result.ErrorMessage

        If Not Object.Equals(DictValue(Context.Variables, "player"), "steve") Then Return "player 的值不正确。"
        If Not Object.Equals(DictValue(Context.Variables, "play"), True) Then Return "play 的值不正确。"
        If Convert.ToInt64(DictValue(Context.Variables, "score"), CultureInfo.InvariantCulture) <> 3L Then Return "score 的值不正确。"

        Dim BadContext As New PlugScriptContext()
        Dim BadResult = PlugScriptEngine.Execute(Lines("const x = 1", "x = 2"), BadContext)
        If BadResult.Success Then Return "常量重新赋值没有被拒绝。"
        If BadResult.ErrorLine <> 2 Then Return $"常量错误行号应为 2，实际为 {BadResult.ErrorLine}。"
        Return Nothing
    End Function

    Private Function TestIsland() As String
        Dim Context As New PlugScriptContext()
        Dim Source = Lines(
            "Plugin.AccessRequest = island {",
            "   -permission: camera",
            "      reason: ""打开摄像头来拍照""",
            "      whenToAsk: called",
            "   -permission: storage",
            "      range:",
            "         ""&PCL/xx/xx""",
            "         ""&root""",
            "      reason: ""to find where the Game is stored""",
            "      whenToAsk: firstLaunch, called",
            "   -permission: executeSystemShell",
            "      reason: ""to launch the game""",
            "      restricted:",
            "         ""rm -rf""",
            "         ""mkdir""",
            "      whenToAsk: firstLaunch, called",
            "}",
            "let nested = island {",
            "   outer:",
            "      nested: island {",
            "         a: 1",
            "      }",
            "}",
            "let indented = island",
            "   name: ""indented""",
            "   values:",
            "      one",
            "      two",
            "   // 注释行应被忽略")

        Dim Result = PlugScriptEngine.Execute(Source, Context)
        If Not Result.Success Then Return "执行失败：" & Result.ErrorMessage

        Dim Requests = TryCast(DictValue(Context.Plugin, "AccessRequest"), IList)
        If Requests Is Nothing Then Return "AccessRequest 不是有序列表。"
        If Requests.Count <> 3 Then Return $"AccessRequest 数量应为 3，实际为 {Requests.Count}。"

        Dim Camera = TryCast(Requests(0), IDictionary)
        If Camera Is Nothing Then Return "第一个权限项不是对象。"
        If Not Object.Equals(DictValue(Camera, "permission"), "camera") Then Return "第一个权限项 permission 不正确。"
        If Not Object.Equals(DictValue(Camera, "reason"), "打开摄像头来拍照") Then Return "第一个权限项 reason 不正确。"
        If Not Object.Equals(DictValue(Camera, "whenToAsk"), "called") Then Return "第一个权限项 whenToAsk 不正确。"

        Dim Storage = TryCast(Requests(1), IDictionary)
        If Storage Is Nothing Then Return "第二个权限项不是对象。"
        Dim Ranges = TryCast(DictValue(Storage, "range"), IList)
        If Ranges Is Nothing OrElse Ranges.Count <> 2 Then Return "range 应为 2 项列表。"
        If Not Object.Equals(Ranges(0), "&PCL/xx/xx") OrElse Not Object.Equals(Ranges(1), "&root") Then Return "range 的值不正确。"

        Dim WhenToAsk = TryCast(DictValue(Storage, "whenToAsk"), IList)
        If WhenToAsk Is Nothing OrElse WhenToAsk.Count <> 2 Then Return "whenToAsk 逗号值应为 2 项列表。"
        If Not Object.Equals(WhenToAsk(0), "firstLaunch") OrElse Not Object.Equals(WhenToAsk(1), "called") Then Return "whenToAsk 的值不正确。"

        Dim Restricted = TryCast(DictValue(TryCast(Requests(2), IDictionary), "restricted"), IList)
        If Restricted Is Nothing OrElse Restricted.Count <> 2 Then Return "restricted 应为 2 项列表。"
        If Not Object.Equals(Restricted(0), "rm -rf") OrElse Not Object.Equals(Restricted(1), "mkdir") Then Return "restricted 的值不正确。"

        Dim Nested = TryCast(DictValue(Context.Variables, "nested"), IDictionary)
        If Nested Is Nothing Then Return "嵌套 island 没有生成对象。"
        Dim Outer = TryCast(DictValue(Nested, "outer"), IDictionary)
        If Outer Is Nothing Then Return "嵌套 island 的 outer 不是对象。"
        Dim Inner = TryCast(DictValue(Outer, "nested"), IDictionary)
        If Inner Is Nothing Then Return "嵌套 island 的 nested 不是对象。"
        If Convert.ToInt64(DictValue(Inner, "a"), CultureInfo.InvariantCulture) <> 1L Then Return "嵌套 island 的 a 值不正确。"

        Dim Indented = TryCast(DictValue(Context.Variables, "indented"), IDictionary)
        If Indented Is Nothing Then Return "纯缩进 island 没有生成对象。"
        If Not Object.Equals(DictValue(Indented, "name"), "indented") Then Return "纯缩进 island 的 name 不正确。"
        Dim Values = TryCast(DictValue(Indented, "values"), IList)
        If Values Is Nothing OrElse Values.Count <> 2 Then Return "纯缩进 island 的 values 应为 2 项列表。"
        If Not Object.Equals(Values(0), "one") OrElse Not Object.Equals(Values(1), "two") Then Return "纯缩进 island 的 values 值不正确。"
        Return Nothing
    End Function

    Private Function TestIndentationFunction() As String
        Dim Context As New PlugScriptContext()
        Dim Source = Lines(
            "let x = 0",
            "function bump()",
            "    x = x + 1",
            "    return x",
            "function add(a, b) {",
            "    return a + b",
            "}",
            "let sum = add(2, 3)",
            "bump()",
            "bump()")
        Dim Result = PlugScriptEngine.Execute(Source, Context)
        If Not Result.Success Then Return "执行失败：" & Result.ErrorMessage
        If Convert.ToInt64(DictValue(Context.Variables, "x"), CultureInfo.InvariantCulture) <> 2L Then Return $"x 应为 2，实际为 {PsValues.ToText(DictValue(Context.Variables, "x"))}。"
        If Convert.ToInt64(DictValue(Context.Variables, "sum"), CultureInfo.InvariantCulture) <> 5L Then Return "带参数函数调用结果不正确。"

        Dim AnswerContext As New PlugScriptContext()
        Dim AnswerResult = PlugScriptEngine.ExecuteFunction(
            Lines(
                "let ignored = 1",
                "function answer()",
                "    return 42"),
            AnswerContext,
            "answer")
        If Not AnswerResult.Success Then Return "ExecuteFunction 执行失败：" & AnswerResult.ErrorMessage
        If Convert.ToInt64(AnswerResult.Result, CultureInfo.InvariantCulture) <> 42L Then Return "ExecuteFunction 的返回值不正确。"
        Return Nothing
    End Function

    Private Function TestControlFlow() As String
        Dim Context As New PlugScriptContext()
        Dim Source = Lines(
            "let total = 0",
            "let i = 0",
            "while i < 3 {",
            "   i = i + 1",
            "   total = total + i",
            "}",
            "let message = ""n="" + i",
            "if total == 6 and i == 3 {",
            "   message = message + ""!""",
            "} else {",
            "   message = ""bad""",
            "}",
            "let math = 2 + 3 * 4",
            "if math == 14",
            "   message = message + ""?""",
            "else",
            "   message = ""bad2""")
        Dim Result = PlugScriptEngine.Execute(Source, Context)
        If Not Result.Success Then Return "执行失败：" & Result.ErrorMessage

        If Convert.ToInt64(DictValue(Context.Variables, "total"), CultureInfo.InvariantCulture) <> 6L Then Return "total 应为 6。"
        If Convert.ToInt64(DictValue(Context.Variables, "i"), CultureInfo.InvariantCulture) <> 3L Then Return "i 应为 3。"
        If Convert.ToInt64(DictValue(Context.Variables, "math"), CultureInfo.InvariantCulture) <> 14L Then Return "math 应为 14。"
        If Not Object.Equals(DictValue(Context.Variables, "message"), "n=3!?") Then Return $"message 应为 n=3!?，实际为 {PsValues.ToText(DictValue(Context.Variables, "message"))}。"
        Return Nothing
    End Function

    Private Function TestErrorLine() As String
        Dim Context As New PlugScriptContext()
        Dim Source = Lines(
            "let a = 1",
            "let b = 2",
            "print(undefinedName)")
        Dim Result = PlugScriptEngine.Execute(Source, Context)
        If Result.Success Then Return "未定义标识符没有导致失败。"
        If Result.ErrorLine <> 3 Then Return $"错误行号应为 3，实际为 {Result.ErrorLine}。"
        If Result.ErrorMessage Is Nothing OrElse Not Result.ErrorMessage.StartsWith("第 3 行：") Then Return "错误信息缺少行号前缀。"

        Dim PlaceholderResult = PlugScriptEngine.Execute(Lines("let a = 1", "<...>"), New PlugScriptContext())
        If PlaceholderResult.Success Then Return "占位符 <...> 没有被拒绝。"
        If PlaceholderResult.ErrorLine <> 2 Then Return $"占位符错误行号应为 2，实际为 {PlaceholderResult.ErrorLine}。"
        Return Nothing
    End Function

    Private Function TestBuiltins() As String
        Dim Context As New PlugScriptContext()
        Dim Files As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
        Context.StorageWrite = Sub(Path, Content) Files(Path) = Content
        Context.StorageRead = Function(Path) If(Files.ContainsKey(Path), Files(Path), Nothing)
        Context.StorageExists = Function(Path) Files.ContainsKey(Path)
        Context.StorageDelete = Sub(Path) Files.Remove(Path)
        Context.StorageList = Function() Files.Keys.ToArray()
        Context.PermissionCheck = Function(Name) String.Equals(Name, "camera", StringComparison.OrdinalIgnoreCase)

        Dim Source = Lines(
            "Storage.write(""a.txt"", ""hello"")",
            "let exists = Storage.exists(""a.txt"")",
            "let text = Storage.read(""a.txt"")",
            "let files = Storage.list()",
            "let allowed = Permission.has(""camera"")",
            "print(""存储测试"", text)")
        Dim Result = PlugScriptEngine.Execute(Source, Context)
        If Not Result.Success Then Return "执行失败：" & Result.ErrorMessage
        If Not Object.Equals(DictValue(Context.Variables, "exists"), True) Then Return "Storage.exists 结果不正确。"
        If Not Object.Equals(DictValue(Context.Variables, "text"), "hello") Then Return "Storage.read 结果不正确。"
        If Not Object.Equals(DictValue(Context.Variables, "allowed"), True) Then Return "Permission.has 结果不正确。"
        If Context.Logs.Count = 0 OrElse Context.Logs(Context.Logs.Count - 1) <> "存储测试 hello" Then Return "print 没有写入日志。"
        Return Nothing
    End Function

    Private Function TestFullExample() As String
        Dim Source = Lines(
            "let player = ""steve""",
            "const play = True",
            "",
            "function on_first_launch() {",
            "   UI.type = ""new_window""",
            "   UI.FileSize = 20",
            "   Plugin.AccessRequest = island {",
            "   -permission: camera",
            "      reason: ""打开摄像头来拍照""",
            "      whenToAsk: called",
            "",
            "   -permission: storage",
            "      range:",
            "         ""&PCL/xx/xx""",
            "         ""&root""",
            "      reason: ""to find where the Game is stored""",
            "      whenToAsk: firstLaunch, called",
            "",
            "   -permission: executeSystemShell",
            "      reason: ""to launch the game""",
            "      restricted:",
            "         ""rm -rf""",
            "         ""mkdir""",
            "      whenToAsk: firstLaunch, called",
            "   }",
            "}",
            "",
            "function main() {",
            "   print(""hello"")",
            "}")

        Dim Context As New PlugScriptContext()
        Dim MainResult = PlugScriptEngine.Execute(Source, Context, "main")
        If Not MainResult.Success Then Return "main 执行失败：" & MainResult.ErrorMessage
        If Context.Logs.Count = 0 OrElse Context.Logs(0) <> "hello" Then Return "main 没有输出 hello。"

        Dim UiContext As New PlugScriptContext()
        Dim UiResult = PlugScriptEngine.ExecuteFunction(Source, UiContext, "on_first_launch")
        If Not UiResult.Success Then Return "on_first_launch 执行失败：" & UiResult.ErrorMessage
        If Not Object.Equals(DictValue(UiContext.Ui, "type"), "new_window") Then Return "UI.type 没有被赋值。"
        If Convert.ToInt64(DictValue(UiContext.Ui, "FileSize"), CultureInfo.InvariantCulture) <> 20L Then Return "UI.FileSize 没有被赋值。"
        Dim Requests = TryCast(DictValue(UiContext.Plugin, "AccessRequest"), IList)
        If Requests Is Nothing OrElse Requests.Count <> 3 Then Return "完整示例的 AccessRequest 不正确。"
        Return Nothing
    End Function

    Private Function DictValue(Container As Object, Key As String) As Object
        If Container Is Nothing Then Return Nothing
        If TypeOf Container Is IDictionary Then
            Dim Map = DirectCast(Container, IDictionary)
            If Map.Contains(Key) Then Return Map(Key)
            For Each ExistingKey In Map.Keys
                If String.Equals(Convert.ToString(ExistingKey, CultureInfo.InvariantCulture), Key, StringComparison.OrdinalIgnoreCase) Then
                    Return Map(ExistingKey)
                End If
            Next
        End If
        Return Nothing
    End Function

End Module
