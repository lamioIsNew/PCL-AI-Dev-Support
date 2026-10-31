Public Class PagePlugin

    Private Sub PagePlugin_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        '重复加载部分
        PanBack.ScrollToHome()
    End Sub

End Class
