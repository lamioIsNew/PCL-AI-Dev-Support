Imports System.Collections.Generic

Public Class PagePlugin

    Private IsLoadedOnce As Boolean = False
    Private IsScanning As Boolean = False
    Private LastPlugins As New List(Of PlugScriptPlugin)()

    Private Sub PagePlugin_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        '重复加载部分
        PanBack.ScrollToHome()

        If Not IsLoadedOnce Then
            IsLoadedOnce = True
            LabPluginPath.Text = PlugScriptHost.PluginRoot
        End If

        '每次进入页面都从宿主缓存刷新；源文件未变化时不会重复执行 on_first_launch。
        RefreshPluginList(False)
    End Sub

#Region "扫描与刷新"

    Private Sub RefreshPluginList(Force As Boolean)
        If IsScanning Then Return
        IsScanning = True
        LabPluginStatus.Text = If(Force, "正在重新扫描插件...", "正在扫描插件...")

        RunInThread(
        Sub()
            Dim Plugins As List(Of PlugScriptPlugin)
            Try
                If Force Then PlugScriptHost.ClearCache()
                Plugins = PlugScriptHost.ScanPlugins(False)
            Catch ex As Exception
                Logger.Error(ex, "扫描 PlugScript 插件失败")
                RunInUi(
                Sub()
                    IsScanning = False
                    LabPluginStatus.Text = "扫描插件失败：" & ex.Message
                    RenderPlugins(New List(Of PlugScriptPlugin)())
                End Sub)
                Return
            End Try

            RunInUi(
            Sub()
                IsScanning = False
                RenderPlugins(Plugins)
            End Sub)
        End Sub)
    End Sub

    Private Sub RenderPlugins(Plugins As List(Of PlugScriptPlugin))
        If Plugins Is Nothing Then Plugins = New List(Of PlugScriptPlugin)()
        LastPlugins = Plugins
        PanPlugins.Children.Clear()

        If Plugins.Count = 0 Then
            PanPlugins.Children.Add(BuildEmptyCard())
            LabPluginStatus.Text = "未找到插件。你可以点击「创建示例插件」，或手动把插件文件夹放进上面的目录。"
            Return
        End If

        For Each Plugin In Plugins
            Try
                PanPlugins.Children.Add(BuildPluginCard(Plugin))
            Catch ex As Exception
                Logger.Error(ex, "生成 PlugScript 插件卡片失败：" & If(Plugin Is Nothing, "", Plugin.Name))
            End Try
        Next

        LabPluginStatus.Text = $"共找到 {Plugins.Count} 个插件。"
    End Sub

#End Region

#Region "界面构建"

    Private Function BuildEmptyCard() As MyCard
        Dim Card As New MyCard With {.Title = "暂无插件", .Margin = New Thickness(0, 0, 0, 15)}
        Dim Panel As New StackPanel With {.Margin = New Thickness(25, 42, 25, 22)}
        Panel.Children.Add(CreateLabel("插件目录为空。", False, True, 14))
        Panel.Children.Add(CreateLabel("每个插件应放在独立的子文件夹中，并包含 plugin.plugscript。点击上方「创建示例插件」可以生成一个可运行的 HelloWindow 示例。", True, False, 12, New Thickness(0, 6, 0, 0)))
        Card.Children.Add(Panel)
        Return Card
    End Function

    Private Function BuildPluginCard(Plugin As PlugScriptPlugin) As MyCard
        Dim Card As New MyCard With {.Title = Plugin.Name, .Margin = New Thickness(0, 0, 0, 15)}
        Dim Panel As New StackPanel With {.Margin = New Thickness(25, 42, 25, 22)}
        Card.Children.Add(Panel)

        '状态
        Dim IsError = Plugin.Status = "加载失败"
        Dim StatusText = CreateLabel("状态：" & Plugin.Status, IsError, True, 14)
        If IsError Then
            StatusText.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrushRedDark")
        ElseIf Plugin.Status = "已加载" Then
            StatusText.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush3")
        End If
        Panel.Children.Add(StatusText)

        '基础信息
        Panel.Children.Add(CreateLabel("入口：" & Plugin.SourcePath, True, False, 12, New Thickness(0, 6, 0, 0)))
        Panel.Children.Add(CreateLabel("UI 类型：" & PlugScriptHost.DescribeUi(Plugin), True, False, 12, New Thickness(0, 4, 0, 0)))

        If Plugin.HasWindow Then
            Panel.Children.Add(CreateLabel(
                $"窗口尺寸：{Plugin.Width:0} × {Plugin.Height:0}，标题：{If(String.IsNullOrWhiteSpace(Plugin.Title), Plugin.Name, Plugin.Title)}",
                True, False, 12, New Thickness(0, 4, 0, 0)))
        End If

        Panel.Children.Add(CreateLabel(
            $"存储配额：{Plugin.FileSizeMb:0.#} MB（插件目录内的文件）",
            True, False, 12, New Thickness(0, 4, 0, 0)))

        '权限
        Panel.Children.Add(CreateLabel("权限：" & PlugScriptHost.GetPermissionSummary(Plugin), True, False, 12, New Thickness(0, 4, 0, 0)))
        If Plugin.Permissions IsNot Nothing Then
            For Each Request In Plugin.Permissions
                Panel.Children.Add(CreateLabel("· " & PlugScriptHost.DescribePermission(Request), True, False, 11, New Thickness(8, 2, 0, 0)))
            Next
        End If

        '加载错误
        If IsError AndAlso Not String.IsNullOrWhiteSpace(Plugin.ErrorMessage) Then
            Dim ErrorText = CreateLabel("错误：" & Plugin.ErrorMessage, True, False, 12, New Thickness(0, 8, 0, 0))
            ErrorText.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrushRedDark")
            Panel.Children.Add(ErrorText)
        End If

        '最近一次 main 的结果
        If Not String.IsNullOrWhiteSpace(Plugin.LastMainMessage) Then
            Dim IsMainError = Plugin.LastMainMessage.Contains("失败") OrElse Plugin.LastMainMessage.Contains("异常")
            Dim MainText = CreateLabel(Plugin.LastMainMessage, Not IsMainError, False, 12, New Thickness(0, 8, 0, 0))
            If IsMainError Then MainText.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrushRedDark")
            Panel.Children.Add(MainText)
        End If

        '按钮
        Dim ButtonPanel As New StackPanel With {.Orientation = Orientation.Horizontal, .Margin = New Thickness(0, 14, 0, 0)}

        If Plugin.HasWindow Then
            Dim BtnOpen As New MyButton With {
                .Text = "打开窗口",
                .MinWidth = 110,
                .Height = 32,
                .Padding = New Thickness(13, 0, 13, 0),
                .ColorType = MyButton.ColorState.Highlight
            }
            AddHandler BtnOpen.Click, Sub(sender, e) PlugScriptHost.OpenPluginWindow(Plugin, Sub() RenderPlugins(LastPlugins))
            ButtonPanel.Children.Add(BtnOpen)
        End If

        Dim BtnRun As New MyButton With {
            .Text = "运行 main",
            .MinWidth = 110,
            .Height = 32,
            .Padding = New Thickness(13, 0, 13, 0)
        }
        If ButtonPanel.Children.Count > 0 Then BtnRun.Margin = New Thickness(12, 0, 0, 0)
        AddHandler BtnRun.Click, Sub(sender, e) RunPluginMain(Plugin)
        ButtonPanel.Children.Add(BtnRun)

        Dim BtnLogs As New MyButton With {
            .Text = "查看日志",
            .MinWidth = 110,
            .Height = 32,
            .Margin = New Thickness(12, 0, 0, 0),
            .Padding = New Thickness(13, 0, 13, 0)
        }
        AddHandler BtnLogs.Click, Sub(sender, e) PlugScriptHost.ShowPluginLogs(Plugin)
        ButtonPanel.Children.Add(BtnLogs)

        Dim BtnFolder As New MyButton With {
            .Text = "打开文件夹",
            .MinWidth = 120,
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
        ButtonPanel.Children.Add(BtnFolder)

        Panel.Children.Add(ButtonPanel)
        Return Card
    End Function

    Private Function CreateLabel(Text As String, Gray As Boolean, Bold As Boolean, Size As Double, Optional Margin As Thickness = Nothing) As TextBlock
        Dim Result As New TextBlock With {
            .Text = If(Text, ""),
            .FontSize = Size,
            .FontWeight = If(Bold, FontWeights.Bold, FontWeights.Normal),
            .TextWrapping = TextWrapping.Wrap,
            .LineHeight = 19,
            .Margin = Margin
        }
        Result.SetResourceReference(TextBlock.ForegroundProperty, If(Gray, "ColorBrushGray2", "ColorBrush1"))
        Return Result
    End Function

#End Region

#Region "按钮事件"

    Private Sub BtnOpenPluginFolder_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnOpenPluginFolder.Click
        Try
            PlugScriptHost.EnsurePluginRoot()
            OpenExplorer(PlugScriptHost.PluginRoot)
        Catch ex As Exception
            Logger.Error(ex, "打开 PlugScript 插件目录失败", LogBehavior.Toast)
        End Try
    End Sub

    Private Sub BtnRescanPlugins_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnRescanPlugins.Click
        RefreshPluginList(True)
    End Sub

    Private Sub BtnCreateSamplePlugin_Click(sender As Object, e As MouseButtonEventArgs) Handles BtnCreateSamplePlugin.Click
        Try
            If PlugScriptHost.CreateSamplePlugin() Then
                LabPluginStatus.Text = "已创建示例插件 HelloWindow，正在刷新列表..."
                RefreshPluginList(True)
            Else
                LabPluginStatus.Text = "HelloWindow 已存在，未覆盖现有文件。你可以直接点击「重新扫描」查看它。"
            End If
        Catch ex As Exception
            Logger.Error(ex, "创建 PlugScript 示例插件失败", LogBehavior.Toast)
            LabPluginStatus.Text = "创建示例插件失败：" & ex.Message
        End Try
    End Sub

    Private Sub RunPluginMain(Plugin As PlugScriptPlugin)
        If Plugin Is Nothing Then Return
        LabPluginStatus.Text = $"正在运行 {Plugin.Name} 的 main..."
        RunInThread(
        Sub()
            Dim Result = PlugScriptHost.RunMain(Plugin)
            RunInUi(
            Sub()
                RenderPlugins(LastPlugins)
                If Result.Success Then
                    LabPluginStatus.Text = $"{Plugin.Name} 的 main 运行成功，日志已更新。"
                Else
                    LabPluginStatus.Text = $"{Plugin.Name} 的 main 运行失败：{Result.ErrorMessage}"
                End If
            End Sub)
        End Sub)
    End Sub

#End Region

End Class
