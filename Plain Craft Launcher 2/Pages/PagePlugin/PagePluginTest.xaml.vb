Public Class PagePluginTest

    Private IsLoadedOnce As Boolean = False

    Private Const DynamicCardXaml As String =
        "<local:MyCard xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"" xmlns:local=""clr-namespace:PCL;assembly=Plain Craft Launcher 2"" Margin=""0,0,0,10"" Title=""动态挂载成功"">" &
        "<StackPanel Margin=""25,42,25,20"">" &
        "<TextBlock Text=""这个卡片由 XAML 字符串在运行时创建，并已挂载到当前页面。"" TextWrapping=""Wrap"" />" &
        "<local:MyButton Text=""动态创建的按钮"" MinWidth=""160"" Height=""30"" HorizontalAlignment=""Left"" Margin=""0,12,0,0"" ColorType=""Highlight"" />" &
        "</StackPanel>" &
        "</local:MyCard>"

    Private Const DynamicWindowXaml As String =
        "<StackPanel xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"" xmlns:local=""clr-namespace:PCL;assembly=Plain Craft Launcher 2"" Margin=""25,20"">" &
        "<TextBlock Text=""动态窗口内容"" FontSize=""18"" FontWeight=""Bold"" />" &
        "<TextBlock Margin=""0,8,0,0"" TextWrapping=""Wrap"" Foreground=""{DynamicResource ColorBrushGray2}"" Text=""这是由 XAML 字符串动态加载的窗口内容。窗口标题由调用方指定，并以 PCL 子窗口的形式显示。"" />" &
        "<local:MyCard Margin=""0,16,0,0"" Title=""窗口内的动态卡片"">" &
        "<StackPanel Margin=""25,42,25,20"">" &
        "<TextBlock Text=""窗口内容与页面内容共用同一套动态 UI 加载逻辑。"" TextWrapping=""Wrap"" />" &
        "</StackPanel>" &
        "</local:MyCard>" &
        "</StackPanel>"

    Private Sub PagePluginTest_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        '重复加载部分
        PanBack.ScrollToHome()

        '非重复加载部分
        If IsLoadedOnce Then Return
        IsLoadedOnce = True
        Try
            MountDynamicUi(PanDynamic, DynamicCardXaml)
        Catch ex As Exception
            Logger.Error(ex, "动态挂载 UI 失败", LogBehavior.Toast)
        End Try
    End Sub

    Private Sub BtnOpenWindow_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnOpenWindow.Click
        Try
            ShowDynamicWindow("PCL - DTA 动态 UI 测试窗口", DynamicWindowXaml)
        Catch ex As Exception
            Logger.Error(ex, "打开动态窗口失败", LogBehavior.Toast)
        End Try
    End Sub

End Class
