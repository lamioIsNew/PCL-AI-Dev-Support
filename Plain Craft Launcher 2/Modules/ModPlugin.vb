''' <summary>
''' 插件相关的动态 UI 加载与子窗口显示。
''' </summary>
Public Module ModPlugin

    ''' <summary>
    ''' 将 XAML 字符串解析为可挂载的 UI 元素。
    ''' </summary>
    Public Function LoadDynamicUi(Xaml As String) As FrameworkElement
        If String.IsNullOrWhiteSpace(Xaml) Then Throw New ArgumentException("XAML 内容不能为空。", NameOf(Xaml))
        Dim Ui = GetObjectFromXML(Xaml)
        Dim Element = TryCast(Ui, FrameworkElement)
        If Element Is Nothing Then Throw New InvalidOperationException("动态 UI 的根元素必须是 FrameworkElement。")
        Return Element
    End Function

    ''' <summary>
    ''' 将动态 UI 挂载到指定的父控件中并显示。
    ''' </summary>
    Public Sub MountDynamicUi(Parent As Panel, Xaml As String)
        If Parent Is Nothing Then Throw New ArgumentNullException(NameOf(Parent))
        Parent.Children.Add(LoadDynamicUi(Xaml))
    End Sub

    ''' <summary>
    ''' 使用指定的标题弹出一个 PCL 风格子窗口，内容由 XAML 字符串动态创建。
    ''' </summary>
    Public Function ShowDynamicWindow(Title As String, Xaml As String) As MyChildWindow
        Return ShowDynamicWindow(Title, LoadDynamicUi(Xaml))
    End Function

    ''' <summary>
    ''' 使用指定的标题弹出一个 PCL 风格子窗口。
    ''' </summary>
    Public Function ShowDynamicWindow(Title As String, Content As FrameworkElement) As MyChildWindow
        If Content Is Nothing Then Throw New ArgumentNullException(NameOf(Content))
        Dim Result As MyChildWindow = Nothing
        RunInUiWait(
        Sub()
            Dim Win As New MyChildWindow(Title, Content)
            If FrmMain IsNot Nothing AndAlso FrmMain.IsLoaded Then Win.Owner = FrmMain
            Win.Show()
            Result = Win
        End Sub)
        Return Result
    End Function

End Module
