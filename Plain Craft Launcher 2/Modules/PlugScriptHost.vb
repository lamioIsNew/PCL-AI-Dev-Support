' PlugScript 插件宿主：扫描插件目录、加载 on_first_launch、提供受限存储与权限信息。
Option Strict Off

Imports System.Collections
Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports System.Text

''' <summary>
''' 插件在 Plugin.AccessRequest 中声明的一项权限。
''' </summary>
Public Class PlugScriptPermissionRequest

    ''' <summary>权限名，例如 camera、storage、executeSystemShell。</summary>
    Public Property Permission As String = ""

    ''' <summary>插件向用户说明的申请理由。</summary>
    Public Property Reason As String = ""

    ''' <summary>申请时机，例如 firstLaunch、called。</summary>
    Public Property WhenToAsk As String = ""

    ''' <summary>storage 权限声明的路径范围。</summary>
    Public Property Range As String = ""

    ''' <summary>是否为受限权限声明。</summary>
    Public Property Restricted As Boolean = False

    ''' <summary>该权限在当前版本是否仅作声明、不会真正授予。</summary>
    Public ReadOnly Property IsDeclaredOnly As Boolean
        Get
            Return String.Equals(Permission, "camera", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(Permission, "executeSystemShell", StringComparison.OrdinalIgnoreCase) OrElse
                   String.Equals(Permission, "storage", StringComparison.OrdinalIgnoreCase)
        End Get
    End Property

End Class

''' <summary>
''' 一个已扫描到的 PlugScript 插件。
''' </summary>
Public Class PlugScriptPlugin

    Public Property Name As String = ""
    Public Property Folder As String = ""
    Public Property SourcePath As String = ""
    Public Property Source As String = ""
    Public Property SourceMtimeUtc As Date

    Public Property Context As PlugScriptContext
    Public Property Status As String = "未加载"
    Public Property ErrorMessage As String = ""
    Public Property ErrorLine As Integer = 0
    Public Property LastMainMessage As String = ""

    Public Property UiType As String = ""
    Public Property Title As String = ""
    Public Property Width As Double = 720
    Public Property Height As Double = 460
    Public Property FileSizeMb As Double = 10
    Public Property StorageQuotaBytes As Long = 10L * 1024L * 1024L

    Public Property Version As String = ""
    Public Property Permissions As New List(Of PlugScriptPermissionRequest)()
    Public Property Logs As New List(Of String)()
    Public Property MainRunCount As Integer = 0

    ''' <summary>是否正在执行 on_first_launch。加载期间允许按声明值逐步收紧配额。</summary>
    Public Property IsLoading As Boolean = False

    ''' <summary>当前插件是否声明了独立窗口 UI。</summary>
    Public ReadOnly Property HasWindow As Boolean
        Get
            Return String.Equals(UiType, "new_window", StringComparison.OrdinalIgnoreCase)
        End Get
    End Property

End Class

''' <summary>
''' PlugScript 插件宿主。所有插件均位于 PCL\Plugin\&lt;PluginName&gt;\ 下。
''' </summary>
Public Module PlugScriptHost

    Private Const DefaultQuotaMb As Double = 10
    Private Const MaxLogLines As Integer = 200

    Private ReadOnly _Cache As New Dictionary(Of String, PlugScriptPlugin)(StringComparer.OrdinalIgnoreCase)
    Private ReadOnly _CacheLock As New Object()

    ''' <summary>插件根目录，始终以反斜杠结尾。</summary>
    Public ReadOnly Property PluginRoot As String
        Get
            Return Paths.Base & "PCL\Plugin\"
        End Get
    End Property

    ''' <summary>宿主状态文件目录，位于插件根目录下，避免污染插件自身文件。</summary>
    Public ReadOnly Property StateFolder As String
        Get
            Return Path.Combine(PluginRoot, ".state")
        End Get
    End Property

#Region "扫描与加载"

    ''' <summary>确保插件根目录存在。</summary>
    Public Sub EnsurePluginRoot()
        Directory.CreateDirectory(PluginRoot)
    End Sub

    ''' <summary>
    ''' 扫描并加载插件。已在本会话中加载且源文件未变化的插件不会重复调用 on_first_launch。
    ''' </summary>
    Public Function ScanPlugins(Optional Force As Boolean = False) As List(Of PlugScriptPlugin)
        Dim Result As New List(Of PlugScriptPlugin)()
        EnsurePluginRoot()

        Dim Folders = Directory.GetDirectories(PluginRoot)
        For Each Folder In Folders
            Dim Name = Path.GetFileName(Folder)
            ' .state 等以点开头的目录不是插件。
            If String.IsNullOrWhiteSpace(Name) OrElse Name.StartsWith(".", StringComparison.Ordinal) Then Continue For

            Dim Entry = FindEntryFile(Folder)
            If Entry Is Nothing Then Continue For

            Try
                Dim Mtime = File.GetLastWriteTimeUtc(Entry)
                SyncLock _CacheLock
                    Dim Plugin As PlugScriptPlugin = Nothing
                    If Not Force AndAlso _Cache.TryGetValue(Name, Plugin) AndAlso
                       String.Equals(Plugin.SourcePath, Entry, StringComparison.OrdinalIgnoreCase) AndAlso
                       Plugin.SourceMtimeUtc = Mtime Then
                        Result.Add(Plugin)
                        Continue For
                    End If

                    Plugin = New PlugScriptPlugin With {
                        .Name = Name,
                        .Folder = Folder.TrimEnd(Path.DirectorySeparatorChar) & Path.DirectorySeparatorChar,
                        .SourcePath = Entry,
                        .SourceMtimeUtc = Mtime
                    }
                    LoadPlugin(Plugin)
                    _Cache(Name) = Plugin
                    Result.Add(Plugin)
                End SyncLock
            Catch ex As Exception
                Logger.Warn(ex, "加载 PlugScript 插件失败：" & Name)
            End Try
        Next

        Return Result.OrderBy(Function(p) p.Name, StringComparer.OrdinalIgnoreCase).ToList()
    End Function

    ''' <summary>清空会话缓存，下一次扫描会重新执行 on_first_launch。</summary>
    Public Sub ClearCache()
        SyncLock _CacheLock
            _Cache.Clear()
        End SyncLock
    End Sub

    Private Function FindEntryFile(Folder As String) As String
        Dim Preferred = Path.Combine(Folder, "plugin.plugscript")
        If File.Exists(Preferred) Then Return Preferred

        Try
            Return Directory.GetFiles(Folder, "*.plugscript", SearchOption.TopDirectoryOnly).
                OrderBy(Function(path) path, StringComparer.OrdinalIgnoreCase).
                FirstOrDefault()
        Catch
            Return Nothing
        End Try
    End Function

    Private Sub LoadPlugin(Plugin As PlugScriptPlugin)
        Plugin.IsLoading = True
        Try
            Plugin.Source = File.ReadAllText(Plugin.SourcePath, New UTF8Encoding(False))
            Dim Context As New PlugScriptContext()
            Context.PluginName = Plugin.Name
            Context.PluginFolder = Plugin.Folder
            Plugin.Context = Context
            AttachDelegates(Plugin)

            Dim Result = PlugScriptEngine.Execute(Plugin.Source, Context, "on_first_launch")
            AppendContextLogs(Plugin, "[首次加载]")

            If Result.Success Then
                ApplyUi(Plugin)
                Plugin.Permissions = ParsePermissions(Context)
                Plugin.Version = SafeText(Context.Plugin("Version"))
                Plugin.Status = "已加载"
                Plugin.ErrorMessage = ""
                Plugin.ErrorLine = 0
            Else
                Plugin.Status = "加载失败"
                Plugin.ErrorMessage = Result.ErrorMessage
                Plugin.ErrorLine = Result.ErrorLine
                ' 即使加载失败，也尽量保留已经解析出的权限声明以便排查。
                Plugin.Permissions = ParsePermissions(Context)
            End If

            Context.MaxStorageBytes = Plugin.StorageQuotaBytes
            PersistState(Plugin)
        Catch ex As Exception
            Plugin.Status = "加载失败"
            Plugin.ErrorMessage = ex.Message
            Plugin.ErrorLine = 0
            Logger.Error(ex, "PlugScript 插件加载异常：" & Plugin.Name)
        Finally
            Plugin.IsLoading = False
        End Try
    End Sub

    Private Sub ApplyUi(Plugin As PlugScriptPlugin)
        Dim Context = Plugin.Context
        Plugin.UiType = SafeText(Context.Ui("type")).Trim()
        Plugin.Title = SafeText(Context.Ui("title")).Trim()

        Dim Width = SafeNumber(Context.Ui("width"), 720)
        Dim Height = SafeNumber(Context.Ui("height"), 460)
        Dim FileSize = SafeNumber(Context.Ui("FileSize"), 0)

        Plugin.Width = If(Width > 0, Width, 720)
        Plugin.Height = If(Height > 0, Height, 460)
        Plugin.FileSizeMb = If(FileSize > 0, FileSize, DefaultQuotaMb)
        Plugin.StorageQuotaBytes = CLng(Math.Round(Plugin.FileSizeMb * 1024D * 1024D))
        If Plugin.StorageQuotaBytes <= 0 Then Plugin.StorageQuotaBytes = CLng(DefaultQuotaMb * 1024D * 1024D)
    End Sub

    Private Sub AttachDelegates(Plugin As PlugScriptPlugin)
        Dim Context = Plugin.Context
        Context.StorageRead = Function(RelativePath) ReadStorage(Plugin, RelativePath)
        Context.StorageWrite = Sub(RelativePath, Text) WriteStorage(Plugin, RelativePath, Text)
        Context.StorageExists = Function(RelativePath) ExistsStorage(Plugin, RelativePath)
        Context.StorageDelete = Sub(RelativePath) DeleteStorage(Plugin, RelativePath)
        Context.StorageList = Function() ListStorage(Plugin)
        ' v0.1 不持久化任何授权，因此所有权限查询都返回 False。
        Context.PermissionCheck = Function(PermissionName) False
    End Sub

    Private Sub AppendContextLogs(Plugin As PlugScriptPlugin, Prefix As String)
        If Plugin.Context Is Nothing Then Return
        For Each Line In Plugin.Context.Logs
            Dim Text = Prefix & " " & If(Line, "")
            Plugin.Logs.Add(Text)
            Logger.Info("PlugScript[" & Plugin.Name & "] " & If(Line, ""))
        Next
        Plugin.Context.Logs.Clear()
        While Plugin.Logs.Count > MaxLogLines
            Plugin.Logs.RemoveAt(0)
        End While
    End Sub

#End Region

#Region "权限解析"

    Private Function ParsePermissions(Context As PlugScriptContext) As List(Of PlugScriptPermissionRequest)
        Dim Result As New List(Of PlugScriptPermissionRequest)()
        If Context Is Nothing Then Return Result

        Dim Raw = Context.Plugin("AccessRequest")
        If Raw Is Nothing OrElse TypeOf Raw Is String Then Return Result
        If Not TypeOf Raw Is IEnumerable Then Return Result

        For Each Item In CType(Raw, IEnumerable)
            If Not TypeOf Item Is IDictionary Then Continue For
            Dim Map = DirectCast(Item, IDictionary)
            Dim PermissionName = GetMapString(Map, "permission").Trim().ToLowerInvariant()
            If PermissionName.Length = 0 Then Continue For

            Result.Add(New PlugScriptPermissionRequest With {
                .Permission = PermissionName,
                .Reason = GetMapString(Map, "reason"),
                .WhenToAsk = DescribeValueList(GetMapValue(Map, "whenToAsk")),
                .Range = DescribeValueList(GetMapValue(Map, "range")),
                .Restricted = GetMapBoolean(Map, "restricted")
            })
        Next

        Return Result
    End Function

    Private Function GetMapValue(Map As IDictionary, Name As String) As Object
        If Map Is Nothing Then Return Nothing
        If Map.Contains(Name) Then Return Map(Name)
        For Each Key In Map.Keys
            If String.Equals(Convert.ToString(Key), Name, StringComparison.OrdinalIgnoreCase) Then Return Map(Key)
        Next
        Return Nothing
    End Function

    Private Function GetMapString(Map As IDictionary, Name As String) As String
        Return SafeText(GetMapValue(Map, Name))
    End Function

    Private Function GetMapBoolean(Map As IDictionary, Name As String) As Boolean
        Dim Value = GetMapValue(Map, Name)
        If TypeOf Value Is Boolean Then Return CBool(Value)
        Return False
    End Function

    Private Function DescribeValueList(Value As Object) As String
        If Value Is Nothing Then Return ""
        If TypeOf Value Is String Then Return CStr(Value)
        If TypeOf Value Is IEnumerable Then
            Dim Parts As New List(Of String)()
            For Each Item In CType(Value, IEnumerable)
                Parts.Add(SafeText(Item))
            Next
            Return String.Join(", ", Parts)
        End If
        Return SafeText(Value)
    End Function

    ''' <summary>权限摘要，供插件卡片显示。</summary>
    Public Function GetPermissionSummary(Plugin As PlugScriptPlugin) As String
        If Plugin Is Nothing OrElse Plugin.Permissions Is Nothing OrElse Plugin.Permissions.Count = 0 Then Return "未声明权限"
        Return String.Join("、", Plugin.Permissions.Select(Function(p) p.Permission))
    End Function

    ''' <summary>单条权限的详细说明。</summary>
    Public Function DescribePermission(Request As PlugScriptPermissionRequest) As String
        If Request Is Nothing Then Return ""
        Dim Parts As New List(Of String) From {Request.Permission}
        If Not String.IsNullOrWhiteSpace(Request.Reason) Then Parts.Add("理由：" & Request.Reason)
        If Not String.IsNullOrWhiteSpace(Request.WhenToAsk) Then Parts.Add("时机：" & Request.WhenToAsk)
        If Not String.IsNullOrWhiteSpace(Request.Range) Then Parts.Add("范围：" & Request.Range)
        If String.Equals(Request.Permission, "storage", StringComparison.OrdinalIgnoreCase) Then
            Parts.Add("仅声明，插件目录外未开放")
        Else
            Parts.Add("仅声明，未开放")
        End If
        Return String.Join(" · ", Parts)
    End Function

#End Region

#Region "受限存储"

    ' 存储根目录就是插件文件夹本身（与 PlugScriptGuide.md 一致）。
    ' 入口文件 plugin.plugscript 不计入 Storage.list()，也不计入配额占用；其他文件全部计入。

    Private Function ResolveStoragePath(Plugin As PlugScriptPlugin, RelativePath As String) As String
        If Plugin Is Nothing Then Throw New ArgumentException("插件上下文无效。")
        If String.IsNullOrWhiteSpace(RelativePath) Then Throw New ArgumentException("存储路径不能为空。")

        Dim Relative = RelativePath.Trim().Replace("/"c, "\"c)
        If Path.IsPathRooted(Relative) OrElse Relative.IndexOf(":"c) >= 0 Then
            Throw New ArgumentException("存储路径不能是绝对路径：" & RelativePath)
        End If

        Dim Root = Path.GetFullPath(Plugin.Folder).TrimEnd("\"c)
        Dim FullPath = Path.GetFullPath(Path.Combine(Root, Relative))
        Dim RootPrefix = Root & "\"
        If Not FullPath.StartsWith(RootPrefix, StringComparison.OrdinalIgnoreCase) Then
            Throw New ArgumentException("存储路径不能超出插件目录：" & RelativePath)
        End If
        If Not String.IsNullOrEmpty(Plugin.SourcePath) AndAlso
           String.Equals(FullPath, Path.GetFullPath(Plugin.SourcePath), StringComparison.OrdinalIgnoreCase) Then
            Throw New ArgumentException("插件不能覆盖自己的入口文件。")
        End If

        Return FullPath
    End Function

    Private Function ReadStorage(Plugin As PlugScriptPlugin, RelativePath As String) As String
        Dim FullPath = ResolveStoragePath(Plugin, RelativePath)
        If Not File.Exists(FullPath) Then Throw New FileNotFoundException("插件存储文件不存在：" & RelativePath)
        Return File.ReadAllText(FullPath, New UTF8Encoding(False))
    End Function

    Private Sub WriteStorage(Plugin As PlugScriptPlugin, RelativePath As String, Text As String)
        Dim FullPath = ResolveStoragePath(Plugin, RelativePath)
        Dim NewText = If(Text, "")
        Dim NewBytes = Encoding.UTF8.GetByteCount(NewText)
        Dim OldBytes As Long = If(File.Exists(FullPath), New FileInfo(FullPath).Length, 0)
        Dim Usage = GetStorageUsage(Plugin)
        Dim Quota = GetStorageQuota(Plugin)

        If Quota > 0 AndAlso Usage - OldBytes + NewBytes > Quota Then
            Throw New InvalidOperationException(
                $"插件存储配额不足：{FormatBytes(Usage - OldBytes + NewBytes)} / {FormatBytes(Quota)}。请清理插件目录或调大 UI.FileSize。")
        End If

        Dim Parent = Path.GetDirectoryName(FullPath)
        If Not String.IsNullOrEmpty(Parent) Then Directory.CreateDirectory(Parent)
        File.WriteAllText(FullPath, NewText, New UTF8Encoding(False))
    End Sub

    Private Function ExistsStorage(Plugin As PlugScriptPlugin, RelativePath As String) As Boolean
        Return File.Exists(ResolveStoragePath(Plugin, RelativePath))
    End Function

    Private Sub DeleteStorage(Plugin As PlugScriptPlugin, RelativePath As String)
        Dim FullPath = ResolveStoragePath(Plugin, RelativePath)
        If File.Exists(FullPath) Then File.Delete(FullPath)
    End Sub

    Private Function ListStorage(Plugin As PlugScriptPlugin) As String()
        Dim Root = Path.GetFullPath(Plugin.Folder).TrimEnd("\"c)
        Dim Items As New List(Of String)()
        Try
            For Each FilePath In Directory.GetFiles(Root, "*", SearchOption.AllDirectories)
                ' 入口文件不算插件存储内容。
                If IsEntryFile(Plugin, FilePath) Then Continue For
                Dim Relative = FilePath.Substring(Root.Length).TrimStart("\"c)
                Items.Add(Relative)
            Next
        Catch ex As Exception
            Logger.Warn(ex, "读取插件存储列表失败：" & Plugin.Name)
        End Try
        Return Items.OrderBy(Function(p) p, StringComparer.OrdinalIgnoreCase).ToArray()
    End Function

    Private Function GetStorageQuota(Plugin As PlugScriptPlugin) As Long
        If Plugin Is Nothing Then Return CLng(DefaultQuotaMb * 1024D * 1024D)
        ' 加载期间允许 on_first_launch 按最新声明值收紧配额；加载完成后固定使用宿主保存的配额，
        ' 防止 main 通过修改 UI.FileSize 绕过限制。
        If Plugin.IsLoading AndAlso Plugin.Context IsNot Nothing Then
            Dim Declared = SafeNumber(Plugin.Context.Ui("FileSize"), 0)
            If Declared > 0 Then Return CLng(Math.Round(Declared * 1024D * 1024D))
        End If
        Return If(Plugin.StorageQuotaBytes > 0, Plugin.StorageQuotaBytes, CLng(DefaultQuotaMb * 1024D * 1024D))
    End Function

    Private Function GetStorageUsage(Plugin As PlugScriptPlugin) As Long
        If Plugin Is Nothing OrElse Not Directory.Exists(Plugin.Folder) Then Return 0
        Dim Total As Long = 0
        Try
            For Each FilePath In Directory.GetFiles(Plugin.Folder, "*", SearchOption.AllDirectories)
                If IsEntryFile(Plugin, FilePath) Then Continue For
                Try
                    Total += New FileInfo(FilePath).Length
                Catch
                    ' 单个文件不可读取时忽略其大小，避免影响其他写入。
                End Try
            Next
        Catch ex As Exception
            Logger.Warn(ex, "统计插件存储占用失败：" & Plugin.Name)
        End Try
        Return Total
    End Function

    Private Function IsEntryFile(Plugin As PlugScriptPlugin, FilePath As String) As Boolean
        Return Plugin IsNot Nothing AndAlso Not String.IsNullOrEmpty(Plugin.SourcePath) AndAlso
               String.Equals(Path.GetFullPath(FilePath), Path.GetFullPath(Plugin.SourcePath), StringComparison.OrdinalIgnoreCase)
    End Function

#End Region

#Region "运行 main"

    ''' <summary>按需运行插件的 main()，并收集日志与错误。</summary>
    Public Function RunMain(Plugin As PlugScriptPlugin) As PlugScriptResult
        If Plugin Is Nothing Then
            Return New PlugScriptResult With {.Success = False, .ErrorMessage = "插件不存在。"}
        End If
        If Plugin.Context Is Nothing Then
            Return New PlugScriptResult With {.Success = False, .ErrorMessage = "插件尚未加载。"}
        End If

        SyncLock Plugin
            Try
                Plugin.Context.Logs.Clear()
                Dim Result = PlugScriptEngine.Execute(Plugin.Source, Plugin.Context, "main")
                Plugin.MainRunCount += 1
                AppendContextLogs(Plugin, "[main]")

                If Result.Success Then
                    Plugin.LastMainMessage = $"main 运行成功（第 {Plugin.MainRunCount} 次）。"
                Else
                    Plugin.LastMainMessage = $"main 运行失败：{Result.ErrorMessage}"
                    Logger.Warn("PlugScript[" & Plugin.Name & "] main 运行失败：" & Result.ErrorMessage)
                End If
                Return Result
            Catch ex As Exception
                Plugin.MainRunCount += 1
                Plugin.LastMainMessage = "main 运行异常：" & ex.Message
                Logger.Error(ex, "PlugScript[" & Plugin.Name & "] main 运行异常")
                Return New PlugScriptResult With {.Success = False, .ErrorMessage = ex.Message, .Context = Plugin.Context}
            End Try
        End SyncLock
    End Function

#End Region

#Region "示例插件"

    Private Const SampleSource As String =
        "// HelloWindow：由 PCL - DTA 创建的 PlugScript 示例插件" & vbCrLf &
        "// 位置：PCL\Plugin\HelloWindow\plugin.plugscript" & vbCrLf &
        vbCrLf &
        "function on_first_launch() {" & vbCrLf &
        "   UI.type = ""new_window""" & vbCrLf &
        "   UI.title = ""Hello PlugScript""" & vbCrLf &
        "   UI.width = 720" & vbCrLf &
        "   UI.height = 460" & vbCrLf &
        "   UI.FileSize = 20" & vbCrLf &
        "}" & vbCrLf &
        vbCrLf &
        "function main() {" & vbCrLf &
        "   print(""Hello from HelloWindow!"")" & vbCrLf &
        "}"

    ''' <summary>
    ''' 创建 HelloWindow 示例插件。若入口文件已存在则返回 False，不会覆盖用户文件。
    ''' </summary>
    Public Function CreateSamplePlugin() As Boolean
        EnsurePluginRoot()
        Dim Folder = Path.Combine(PluginRoot, "HelloWindow")
        Dim Entry = Path.Combine(Folder, "plugin.plugscript")
        If File.Exists(Entry) Then Return False

        Directory.CreateDirectory(Folder)
        File.WriteAllText(Entry, SampleSource, New UTF8Encoding(False))
        Logger.Info("已创建 PlugScript 示例插件：" & Entry)
        Return True
    End Function

#End Region

#Region "状态持久化"

    Private Sub PersistState(Plugin As PlugScriptPlugin)
        Try
            If Plugin Is Nothing Then Return
            Directory.CreateDirectory(StateFolder)
            Dim State = New With {
                .name = Plugin.Name,
                .source = Plugin.SourcePath,
                .sourceMtimeUtc = Plugin.SourceMtimeUtc.ToString("O"),
                .status = Plugin.Status,
                .errorMessage = Plugin.ErrorMessage,
                .uiType = Plugin.UiType,
                .title = Plugin.Title,
                .width = Plugin.Width,
                .height = Plugin.Height,
                .fileSizeMb = Plugin.FileSizeMb,
                .permissions = Plugin.Permissions.Select(Function(p) New With {
                    .permission = p.Permission,
                    .reason = p.Reason,
                    .whenToAsk = p.WhenToAsk,
                    .range = p.Range,
                    .restricted = p.Restricted
                }).ToArray()
            }
            Dim FileName = SafeFileName(Plugin.Name) & ".json"
            Dim Json = JsonConvert.SerializeObject(State, Formatting.Indented)
            File.WriteAllText(Path.Combine(StateFolder, FileName), Json, New UTF8Encoding(False))
        Catch ex As Exception
            ' 状态文件只用于诊断，失败不影响插件运行。
            Logger.Warn(ex, "写入 PlugScript 插件状态失败：" & If(Plugin Is Nothing, "", Plugin.Name))
        End Try
    End Sub

    Private Function SafeFileName(Name As String) As String
        Dim Result = If(Name, "plugin")
        For Each InvalidChar In Path.GetInvalidFileNameChars()
            Result = Result.Replace(InvalidChar, "_"c)
        Next
        Return If(Result.Length = 0, "plugin", Result)
    End Function

#End Region

#Region "显示"

    ''' <summary>把 UI 类型转换成用户可读的文字。</summary>
    Public Function DescribeUi(Plugin As PlugScriptPlugin) As String
        If Plugin Is Nothing OrElse String.IsNullOrWhiteSpace(Plugin.UiType) OrElse
           String.Equals(Plugin.UiType, "window", StringComparison.OrdinalIgnoreCase) Then
            Return "无窗口 UI"
        End If
        If Plugin.HasWindow Then Return "new_window（独立窗口）"
        Return Plugin.UiType & "（无窗口 UI）"
    End Function

    ''' <summary>打开插件窗口。仅对声明了 new_window 的插件有效。</summary>
    Public Sub OpenPluginWindow(Plugin As PlugScriptPlugin, Optional OnChanged As Action = Nothing)
        If Plugin Is Nothing OrElse Not Plugin.HasWindow Then Return

        RunInUi(
        Sub()
            Try
                Dim Content = BuildPluginWindowContent(Plugin, OnChanged)
                Dim WindowTitle = If(String.IsNullOrWhiteSpace(Plugin.Title), Plugin.Name, Plugin.Title)
                Dim Win = ShowDynamicWindow(WindowTitle, Content)
                If Win IsNot Nothing Then
                    Win.Width = Clamp(Plugin.Width, 420, 1000)
                    Win.Height = Clamp(Plugin.Height, 300, 800)
                End If
            Catch ex As Exception
                Logger.Error(ex, "打开 PlugScript 插件窗口失败：" & Plugin.Name, LogBehavior.Toast)
            End Try
        End Sub)
    End Sub

    ''' <summary>显示插件日志窗口。</summary>
    Public Sub ShowPluginLogs(Plugin As PlugScriptPlugin)
        If Plugin Is Nothing Then Return
        RunInUi(
        Sub()
            Try
                Dim Root As New Grid With {.Margin = New Thickness(25, 20, 25, 20)}
                Root.RowDefinitions.Add(New RowDefinition With {.Height = GridLength.Auto})
                Root.RowDefinitions.Add(New RowDefinition With {.Height = New GridLength(1, GridUnitType.Star)})

                Dim Header = CreateTextBlock("最近日志 - " & Plugin.Name, 18, FontWeights.Bold, False, New Thickness(0, 0, 0, 10))
                Grid.SetRow(Header, 0)
                Root.Children.Add(Header)

                Dim LogText = CreateTextBlock(FormatLogs(Plugin), 13, FontWeights.Normal, True)
                LogText.FontFamily = New FontFamily("Consolas")
                Dim Scroll As New ScrollViewer With {
                    .VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    .HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                    .Content = LogText
                }
                Grid.SetRow(Scroll, 1)
                Root.Children.Add(Scroll)

                Dim Win = ShowDynamicWindow("插件日志 - " & Plugin.Name, Root)
                If Win IsNot Nothing Then
                    Win.Width = 760
                    Win.Height = 560
                End If
            Catch ex As Exception
                Logger.Error(ex, "打开 PlugScript 日志窗口失败：" & Plugin.Name, LogBehavior.Toast)
            End Try
        End Sub)
    End Sub

    Private Function BuildPluginWindowContent(Plugin As PlugScriptPlugin, OnChanged As Action) As FrameworkElement
        Dim Root As New StackPanel With {.Margin = New Thickness(25, 20, 25, 20)}
        Root.Children.Add(CreateTextBlock(Plugin.Name, 20, FontWeights.Bold, False, New Thickness(0, 0, 0, 4)))

        Dim Version = If(String.IsNullOrWhiteSpace(Plugin.Version), "未提供", Plugin.Version)
        Root.Children.Add(CreateTextBlock($"版本 {Version} · {Plugin.Status} · {DescribeUi(Plugin)}", 12, FontWeights.Normal, True, New Thickness(0, 0, 0, 14)))

        Dim InfoCard = CreateSectionCard("插件信息")
        Dim InfoPanel = GetSectionPanel(InfoCard)
        InfoPanel.Children.Add(CreateTextBlock("插件目录：" & Plugin.Folder, 12, FontWeights.Normal, True))
        InfoPanel.Children.Add(CreateTextBlock($"存储配额：{Plugin.FileSizeMb:0.#} MB，已用 {FormatBytes(GetStorageUsage(Plugin))}", 12, FontWeights.Normal, True))
        InfoPanel.Children.Add(CreateTextBlock("入口文件：" & Plugin.SourcePath, 12, FontWeights.Normal, True))
        Root.Children.Add(InfoCard)

        Dim PermissionCard = CreateSectionCard("权限声明")
        Dim PermissionPanel = GetSectionPanel(PermissionCard)
        If Plugin.Permissions Is Nothing OrElse Plugin.Permissions.Count = 0 Then
            PermissionPanel.Children.Add(CreateTextBlock("该插件没有声明权限。", 12, FontWeights.Normal, True))
        Else
            For Each Request In Plugin.Permissions
                PermissionPanel.Children.Add(CreateTextBlock("· " & DescribePermission(Request), 12, FontWeights.Normal, True))
            Next
        End If
        Root.Children.Add(PermissionCard)

        Dim LogCard = CreateSectionCard("最近日志")
        Dim LogPanel = GetSectionPanel(LogCard)
        Dim LogText = CreateTextBlock(FormatLogs(Plugin), 12, FontWeights.Normal, True)
        LogText.FontFamily = New FontFamily("Consolas")
        Dim LogScroll As New ScrollViewer With {
            .VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            .HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            .MaxHeight = 160,
            .Content = LogText
        }
        LogPanel.Children.Add(LogScroll)
        Root.Children.Add(LogCard)

        Dim RunStatus = CreateTextBlock(If(String.IsNullOrWhiteSpace(Plugin.LastMainMessage), "尚未运行 main。", Plugin.LastMainMessage), 12, FontWeights.Normal, True)
        Root.Children.Add(RunStatus)

        Dim ButtonPanel As New StackPanel With {.Orientation = Orientation.Horizontal, .Margin = New Thickness(0, 12, 0, 0)}
        Dim BtnRun As New MyButton With {
            .Text = "运行 main",
            .MinWidth = 140,
            .Height = 32,
            .Padding = New Thickness(13, 0, 13, 0),
            .ColorType = MyButton.ColorState.Highlight
        }
        Dim BtnFolder As New MyButton With {
            .Text = "打开插件文件夹",
            .MinWidth = 150,
            .Height = 32,
            .Margin = New Thickness(12, 0, 0, 0),
            .Padding = New Thickness(13, 0, 13, 0)
        }
        AddHandler BtnFolder.Click, Sub(sender, e)
                                        Try
                                            OpenExplorer(Plugin.Folder)
                                        Catch ex As Exception
                                            Logger.Warn(ex, "打开插件文件夹失败")
                                        End Try
                                    End Sub
        AddHandler BtnRun.Click, Sub(sender, e)
                                     BtnRun.IsEnabled = False
                                     RunStatus.Text = "正在运行 main..."
                                     RunStatus.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrushGray2")
                                     RunInThread(
                                     Sub()
                                         Dim Result = RunMain(Plugin)
                                         RunInUi(
                                         Sub()
                                             BtnRun.IsEnabled = True
                                             If Result.Success Then
                                                 RunStatus.Text = Plugin.LastMainMessage
                                                 RunStatus.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush3")
                                             Else
                                                 RunStatus.Text = Plugin.LastMainMessage & If(String.IsNullOrWhiteSpace(Result.ErrorMessage), "", "（" & Result.ErrorMessage & "）")
                                                 RunStatus.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrushRedDark")
                                             End If
                                             LogText.Text = FormatLogs(Plugin)
                                             If OnChanged IsNot Nothing Then OnChanged.Invoke()
                                         End Sub)
                                     End Sub)
                                 End Sub
        ButtonPanel.Children.Add(BtnRun)
        ButtonPanel.Children.Add(BtnFolder)
        Root.Children.Add(ButtonPanel)

        Return New ScrollViewer With {
            .VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            .HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            .Content = Root
        }
    End Function

    Private Function CreateSectionCard(Title As String) As MyCard
        Dim Card As New MyCard With {.Title = Title, .Margin = New Thickness(0, 0, 0, 12)}
        Card.Children.Add(New StackPanel With {.Margin = New Thickness(25, 42, 25, 20)})
        Return Card
    End Function

    Private Function GetSectionPanel(Card As MyCard) As StackPanel
        Return DirectCast(Card.Children(Card.Children.Count - 1), StackPanel)
    End Function

    Private Function CreateTextBlock(Text As String, FontSize As Double, Weight As FontWeight, Gray As Boolean, Optional Margin As Thickness = Nothing) As TextBlock
        Dim Result As New TextBlock With {
            .Text = If(Text, ""),
            .FontSize = FontSize,
            .FontWeight = Weight,
            .TextWrapping = TextWrapping.Wrap,
            .LineHeight = 19,
            .Margin = Margin
        }
        Result.SetResourceReference(TextBlock.ForegroundProperty, If(Gray, "ColorBrushGray2", "ColorBrush1"))
        Return Result
    End Function

    Public Function FormatLogs(Plugin As PlugScriptPlugin) As String
        If Plugin Is Nothing OrElse Plugin.Logs Is Nothing OrElse Plugin.Logs.Count = 0 Then Return "暂无日志。"
        Return String.Join(vbCrLf, Plugin.Logs)
    End Function

#End Region

#Region "辅助"

    Public Function FormatBytes(Bytes As Long) As String
        If Bytes < 1024 Then Return Bytes.ToString() & " B"
        If Bytes < 1024L * 1024L Then Return (Bytes / 1024D).ToString("0.#") & " KB"
        Return (Bytes / 1024D / 1024D).ToString("0.##") & " MB"
    End Function

    Private Function Clamp(Value As Double, MinValue As Double, MaxValue As Double) As Double
        If Double.IsNaN(Value) OrElse Double.IsInfinity(Value) Then Return MinValue
        Return Math.Max(MinValue, Math.Min(MaxValue, Value))
    End Function

    Private Function SafeText(Value As Object) As String
        If Value Is Nothing Then Return ""
        Return Convert.ToString(Value)
    End Function

    Private Function SafeNumber(Value As Object, Fallback As Double) As Double
        If Value Is Nothing Then Return Fallback
        If TypeOf Value Is Double OrElse TypeOf Value Is Single OrElse TypeOf Value Is Integer OrElse
           TypeOf Value Is Long OrElse TypeOf Value Is Short OrElse TypeOf Value Is Decimal Then
            Try
                Return Convert.ToDouble(Value)
            Catch
                Return Fallback
            End Try
        End If
        Dim Parsed As Double
        If Double.TryParse(Convert.ToString(Value), Parsed) Then Return Parsed
        Return Fallback
    End Function

#End Region

End Module
