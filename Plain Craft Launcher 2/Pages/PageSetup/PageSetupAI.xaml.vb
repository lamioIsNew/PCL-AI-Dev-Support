Public Class PageSetupAI

    Private Sub PageSetupAI_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        '重复加载部分
        PanBack.ScrollToHome()
    End Sub

    ''' <summary>
    ''' 初始化本页设置。
    ''' </summary>
    Public Sub Reset()
        '该页面暂无可重置的设置项
    End Sub

    Private Sub BtnMcpStart_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnMcpStart.Click
        Hint("Java AI 开发 MCP 功能正在开发中，敬请期待！")
    End Sub

End Class
