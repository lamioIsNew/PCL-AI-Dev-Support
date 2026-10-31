Public Class MyChildWindow

    Public Sub New()
        InitializeComponent()
    End Sub

    ''' <summary>
    ''' 创建一个带有自定义标题和内容的子窗口。
    ''' </summary>
    Public Sub New(TitleText As String, WindowContent As FrameworkElement)
        InitializeComponent()
        Title = TitleText
        LabTitle.Text = TitleText
        PanContent.Content = WindowContent
    End Sub

    Private Sub BtnClose_Click(sender As Object, e As EventArgs) Handles BtnClose.Click
        Close()
    End Sub

    Private Sub PanTitle_MouseLeftButtonDown(sender As Object, e As MouseButtonEventArgs) Handles PanTitle.MouseLeftButtonDown
        If e.ButtonState <> MouseButtonState.Pressed Then Return
        '按钮区域不触发拖动，否则会抢走关闭按钮的 MouseUp
        If BtnClose.IsMouseOver Then Return
        Try
            DragMove()
        Catch ex As Exception
            Logger.Warn(ex, "拖动子窗口失败")
        End Try
    End Sub

End Class
