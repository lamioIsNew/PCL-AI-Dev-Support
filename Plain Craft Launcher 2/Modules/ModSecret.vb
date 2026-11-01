'由于包含加解密等安全信息，本文件中的部分代码已被删除

Friend Module ModSecret

    '标注 PCL 的不同分支，仅用于替换标记
    Public Const VersionBranchMain As String = "OpenSource"
    '在开源版的注册表与常规版的注册表隔离，以防数据冲突
    Public Const RegFolder As String = "PCLDebug"
    '用于微软登录的 ClientId
    Public OAuthClientId As String = If(Environment.GetEnvironmentVariable("PCL_MS_CLIENT_ID"), "")
    'CurseForge API Key
    Public CurseForgeAPIKey As String = If(Environment.GetEnvironmentVariable("PCL_CURSEFORGE_API_KEY"), "")
    '用于匿名数据收集的腾讯云日志服务上报 URL，形如 https://{region}.cls.tencentcs.com/track?topic_id={topic_id}
    Public Const ClsBaseUrl As String = ""

#Region "网络鉴权"

    '是否限制 Minecraft 下载（PCL - DTA 为第三方分支，恢复游戏核心下载与安装功能）
    Public Const RestrictMcDownload As Boolean = False
    Friend Function SecretCdnSign(UrlWithMark As String) As String
        If Not UrlWithMark.EndsWithF("{CDN}") Then Return UrlWithMark
        Return UrlWithMark.Replace("{CDN}", "").Replace(" ", "%20")
    End Function
    ''' <summary>
    ''' 设置 Headers 的 UA、Referer。
    ''' </summary>
    Friend Sub SecretHeadersSign(Url As String, ByRef Req As HttpRequestMessage, Optional SimulateBrowserHeaders As Boolean = False)
        If Not Req.Headers.UserAgent.Any Then
            If Url.Contains("baidupcs.com") OrElse Url.Contains("baidu.com") Then
                Req.Headers.Add("User-Agent", "LogStatistic")  '#4951
            ElseIf SimulateBrowserHeaders Then
                Req.Headers.Add("User-Agent", $"PCL2/{VersionBaseName}.{CInt(BuildType)} Mozilla/5.0 AppleWebKit/537.36 Chrome/63.0.3239.132 Safari/537.36")
            Else
                Req.Headers.Add("User-Agent", $"PCL2/{VersionBaseName}.{CInt(BuildType)}")
            End If
        End If
        If Not SimulateBrowserHeaders Then Req.Headers.Add("Referer", $"http://{VersionCode}.open.pcl2.server/")
        If Url.Contains("api.curseforge.com") OrElse Url.Contains("forgecdn.net") Then Req.Headers.Add("x-api-key", CurseForgeAPIKey)
    End Sub

#End Region

#Region "主题"

    Public Color1 As New MyColor(52, 61, 74)
    Public Color2 As New MyColor(11, 91, 203)
    Public Color3 As New MyColor(19, 112, 243)
    Public Color4 As New MyColor(72, 144, 245)
    Public Color5 As New MyColor(150, 192, 249)
    Public Color6 As New MyColor(213, 230, 253)
    Public Color7 As New MyColor(222, 236, 253)
    Public Color8 As New MyColor(234, 242, 254)
    Public ColorBg0 As New MyColor(150, 192, 249)
    Public ColorBg1 As New MyColor(190, Color7)
    Public ColorGray1 As New MyColor(64, 64, 64)
    Public ColorGray2 As New MyColor(115, 115, 115)
    Public ColorGray3 As New MyColor(140, 140, 140)
    Public ColorGray4 As New MyColor(166, 166, 166)
    Public ColorGray5 As New MyColor(204, 204, 204)
    Public ColorGray6 As New MyColor(235, 235, 235)
    Public ColorGray7 As New MyColor(240, 240, 240)
    Public ColorGray8 As New MyColor(245, 245, 245)
    Public ColorSemiTransparent As New MyColor(1, Color8)
    '深色模式下的各表面颜色
    Public ColorCard As New MyColor(245, 255, 255, 255)
    Public ColorInput As New MyColor(85, 255, 255, 255)
    Public ColorSidebar As New MyColor(241, 255, 255, 255)
    Public ColorOverlay As New MyColor(251, 251, 251)
    Public ColorDivider As New MyColor(34, 64, 64, 64)
    '用于需要白色文字/图标的强调色背景（深色模式下保持足够的对比度）
    Public ColorAccentStrong As New MyColor(11, 91, 203)

    Public ThemeNow As Integer = -1
    Public ThemeIsDark As Boolean = False
    Public ColorHue As Integer = 210, ColorSat As Integer = 85, ColorLightAdjust As Integer = 0, ColorHueTopbarDelta As OneOf(Of Integer, Integer()) = 0
    Public ThemeDontClick As Integer = 0

    Public Sub ThemeRefresh(Optional NewTheme As Integer = -1)
        Try
            '即使主题编号没有改变，也需要刷新，否则切换深色模式时不会生效
            If NewTheme >= 0 Then ThemeNow = NewTheme
            ThemeIsDark = Settings.Get(Of Boolean)("UiDarkMode")

            If ThemeIsDark Then
                '深色模式：主文字接近白色，强调色保持当前色调，背景为带色调的深灰
                Color1 = New MyColor(238, 240, 243)
                Color2 = New MyColor().FromHSL2(ColorHue, ColorSat, 62 + ColorLightAdjust)
                Color3 = New MyColor().FromHSL2(ColorHue, ColorSat, 72 + ColorLightAdjust)
                Color4 = New MyColor().FromHSL2(ColorHue, ColorSat, 60 + ColorLightAdjust)
                Color5 = New MyColor().FromHSL2(ColorHue, ColorSat * 0.8, 82 + ColorLightAdjust * 0.4)
                Color6 = New MyColor().FromHSL(ColorHue, 12, 26)
                Color7 = New MyColor().FromHSL(ColorHue, 11, 28)
                Color8 = New MyColor().FromHSL(ColorHue, 10, 18)
                ColorBg0 = New MyColor().FromHSL2(ColorHue, ColorSat * 0.55, 52 + ColorLightAdjust)
                ColorBg1 = New MyColor(190, New MyColor().FromHSL(ColorHue, 12, 30))
                '灰色系反转：Gray1 为浅色文字，Gray8 为深色表面
                ColorGray1 = New MyColor().FromHSL(ColorHue, 6, 92)
                ColorGray2 = New MyColor().FromHSL(ColorHue, 6, 84)
                ColorGray3 = New MyColor().FromHSL(ColorHue, 6, 76)
                ColorGray4 = New MyColor().FromHSL(ColorHue, 6, 68)
                ColorGray5 = New MyColor().FromHSL(ColorHue, 7, 56)
                ColorGray6 = New MyColor().FromHSL(ColorHue, 8, 36)
                ColorGray7 = New MyColor().FromHSL(ColorHue, 9, 26)
                ColorGray8 = New MyColor().FromHSL(ColorHue, 10, 18)
                ColorCard = New MyColor(245, New MyColor().FromHSL(ColorHue, 11, 21))
                ColorInput = New MyColor().FromHSL(ColorHue, 10, 24)
                ColorSidebar = New MyColor(241, New MyColor().FromHSL(ColorHue, 10, 15))
                ColorOverlay = New MyColor().FromHSL(ColorHue, 10, 19)
                ColorDivider = New MyColor(34, Color1)
                ColorAccentStrong = New MyColor().FromHSL2(ColorHue, ColorSat, 45 + ColorLightAdjust)
            Else
                '浅色模式：保持原有配色
                Color1 = New MyColor().FromHSL2(ColorHue, ColorSat * 0.2, 25 + ColorLightAdjust * 0.3)
                Color2 = New MyColor().FromHSL2(ColorHue, ColorSat, 45 + ColorLightAdjust)
                Color3 = New MyColor().FromHSL2(ColorHue, ColorSat, 55 + ColorLightAdjust)
                Color4 = New MyColor().FromHSL2(ColorHue, ColorSat, 65 + ColorLightAdjust)
                Color5 = New MyColor().FromHSL2(ColorHue, ColorSat, 80 + ColorLightAdjust * 0.4)
                Color6 = New MyColor().FromHSL2(ColorHue, ColorSat, 91 + ColorLightAdjust * 0.1)
                Color7 = New MyColor().FromHSL2(ColorHue, ColorSat, 95)
                Color8 = New MyColor().FromHSL2(ColorHue, ColorSat, 97)
                ColorGray1 = New MyColor(64, 64, 64)
                ColorGray2 = New MyColor(115, 115, 115)
                ColorGray3 = New MyColor(140, 140, 140)
                ColorGray4 = New MyColor(166, 166, 166)
                ColorGray5 = New MyColor(204, 204, 204)
                ColorGray6 = New MyColor(235, 235, 235)
                ColorGray7 = New MyColor(240, 240, 240)
                ColorGray8 = New MyColor(245, 245, 245)
                ColorBg0 = Color4 * 0.4 + Color5 * 0.4 + ColorGray4 * 0.2
                ColorBg1 = New MyColor(190, Color7)
                ColorCard = New MyColor(245, 255, 255, 255)
                ColorInput = New MyColor(85, 255, 255, 255)
                ColorSidebar = New MyColor(241, 255, 255, 255)
                ColorOverlay = New MyColor(251, 251, 251)
                ColorDivider = New MyColor(34, Color1)
                ColorAccentStrong = Color2
            End If

            ColorSemiTransparent = New MyColor(1, Color8)
            WriteThemeColor("ColorBrush1", Color1)
            WriteThemeColor("ColorBrush2", Color2)
            WriteThemeColor("ColorBrush3", Color3)
            WriteThemeColor("ColorBrush4", Color4)
            WriteThemeColor("ColorBrush5", Color5)
            WriteThemeColor("ColorBrush6", Color6)
            WriteThemeColor("ColorBrush7", Color7)
            WriteThemeColor("ColorBrush8", Color8)
            WriteThemeColor("ColorBrushBg0", ColorBg0)
            WriteThemeColor("ColorBrushBg1", ColorBg1)
            For i = 1 To 8
                WriteThemeColor("ColorBrushGray" & i, GetThemeGray(i))
            Next
            WriteThemeColor("ColorBrushSemiTransparent", ColorSemiTransparent)
            WriteThemeColor("ColorBrushCard", ColorCard)
            WriteThemeColor("ColorBrushInput", ColorInput)
            WriteThemeColor("ColorBrushSidebar", ColorSidebar)
            WriteThemeColor("ColorBrushBackgroundTransparentSidebar", ColorSidebar)
            WriteThemeColor("ColorBrushOverlay", ColorOverlay)
            WriteThemeColor("ColorBrushDivider", ColorDivider)
            WriteThemeColor("ColorBrushAccentStrong", ColorAccentStrong)
            Application.Current.Resources("ColorBrushHalfWhite") = New SolidColorBrush(If(ThemeIsDark, New MyColor(38, 255, 255, 255), New MyColor(85, 255, 255, 255)))
            Application.Current.Resources("ColorBrushSemiWhite") = New SolidColorBrush(If(ThemeIsDark, New MyColor(51, 255, 255, 255), New MyColor(187, 255, 255, 255)))
            '校验失败等红色背景在深色模式下需要压暗，避免刺眼
            Application.Current.Resources("ColorBrushRedBack") = New SolidColorBrush(If(ThemeIsDark, New MyColor(128, 96, 32, 32), New MyColor(128, 251, 221, 221)))
            'ColorObject1 主要用作阴影颜色，深色模式下应保持深色，避免出现白色光晕
            Application.Current.Resources("ColorObject1") = If(ThemeIsDark, Colors.Black, CType(Color1, Color))
            Application.Current.Resources("ColorObject2") = CType(Color2, Color)
            Application.Current.Resources("ColorObject3") = CType(Color3, Color)
            Application.Current.Resources("ColorObject4") = CType(Color4, Color)
            Application.Current.Resources("ColorObject5") = CType(Color5, Color)
            Application.Current.Resources("ColorObject6") = CType(Color6, Color)
            Application.Current.Resources("ColorObject7") = CType(Color7, Color)
            Application.Current.Resources("ColorObject8") = CType(Color8, Color)
            Application.Current.Resources("ColorObjectBg0") = CType(ColorBg0, Color)
            Application.Current.Resources("ColorObjectBg1") = CType(ColorBg1, Color)
            For i = 1 To 8
                Application.Current.Resources("ColorObjectGray" & i) = CType(GetThemeGray(i), Color)
            Next
            ThemeRefreshMain()
        Catch ex As Exception
            Logger.Error(ex, "刷新主题颜色失败", LogBehavior.Toast)
        End Try
    End Sub
    ''' <summary>
    ''' 将主题颜色写入应用程序资源，供 DynamicResource 实时更新。
    ''' </summary>
    Private Sub WriteThemeColor(Key As String, Color As MyColor)
        Application.Current.Resources(Key) = New SolidColorBrush(Color)
    End Sub
    ''' <summary>
    ''' 获取当前主题下指定序号的灰色。
    ''' </summary>
    Private Function GetThemeGray(Index As Integer) As MyColor
        Select Case Index
            Case 1 : Return ColorGray1
            Case 2 : Return ColorGray2
            Case 3 : Return ColorGray3
            Case 4 : Return ColorGray4
            Case 5 : Return ColorGray5
            Case 6 : Return ColorGray6
            Case 7 : Return ColorGray7
            Case Else : Return ColorGray8
        End Select
    End Function
    Public Sub ThemeRefreshMain()
        RunInUi(
        Sub()
            If Not FrmMain.IsLoaded Then Return
            '顶部条背景
            Dim Brush = New LinearGradientBrush With {.EndPoint = New Point(1, 0), .StartPoint = New Point(0, 0)}
            Dim Deltas = ColorHueTopbarDelta.Switch(Function(d) New Integer() {-d, 0, d}, Function(d) d)
            Dim TitleLight = If(ThemeIsDark, 38, 48)
            Dim TitleLightCenter = If(ThemeIsDark, 44, 54)
            Brush.GradientStops.Add(New GradientStop With {.Offset = 0, .Color = New MyColor().FromHSL2(ColorHue + Deltas(0), ColorSat, TitleLight + ColorLightAdjust)})
            Brush.GradientStops.Add(New GradientStop With {.Offset = 0.5, .Color = New MyColor().FromHSL2(ColorHue + Deltas(1), ColorSat, TitleLightCenter + ColorLightAdjust)})
            Brush.GradientStops.Add(New GradientStop With {.Offset = 1, .Color = New MyColor().FromHSL2(ColorHue + Deltas(2), ColorSat, TitleLight + ColorLightAdjust)})
            FrmMain.PanTitle.Background = Brush
            FrmMain.PanTitle.Background.Freeze()
            '主页面背景
            If Settings.Get(Of Boolean)("UiBackgroundColorful") Then
                Brush = New LinearGradientBrush With {.EndPoint = New Point(0.1, 1), .StartPoint = New Point(0.9, 0)}
                If ThemeIsDark Then
                    Brush.GradientStops.Add(New GradientStop With {.Offset = -0.1, .Color = New MyColor().FromHSL(ColorHue - 15, ColorSat * 0.5, 12)})
                    Brush.GradientStops.Add(New GradientStop With {.Offset = 0.4, .Color = New MyColor().FromHSL(ColorHue, ColorSat * 0.5, 15)})
                    Brush.GradientStops.Add(New GradientStop With {.Offset = 1.1, .Color = New MyColor().FromHSL(ColorHue + 15, ColorSat * 0.5, 12)})
                Else
                    Brush.GradientStops.Add(New GradientStop With {.Offset = -0.1, .Color = New MyColor().FromHSL2(ColorHue - 15, ColorSat * 0.8, 91)})
                    Brush.GradientStops.Add(New GradientStop With {.Offset = 0.4, .Color = New MyColor().FromHSL2(ColorHue, ColorSat * 0.8, 91)})
                    Brush.GradientStops.Add(New GradientStop With {.Offset = 1.1, .Color = New MyColor().FromHSL2(ColorHue + 15, ColorSat * 0.8, 91)})
                End If
                FrmMain.PanForm.Background = Brush
            Else
                FrmMain.PanForm.Background = If(ThemeIsDark, New MyColor().FromHSL(ColorHue, 10, 13), New MyColor(245, 245, 245))
            End If
            FrmMain.PanForm.Background.Freeze()
        End Sub)
    End Sub
    Friend Sub ThemeCheckAll(EffectSetup As Boolean)
    End Sub
    Friend Function ThemeCheckOne(Id As Integer) As Boolean
        Return True
    End Function
    Friend Function ThemeUnlock(Id As Integer, Optional ShowDoubleHint As Boolean = True, Optional UnlockHint As String = Nothing) As Boolean
        Return False
    End Function

#End Region

#Region "更新"

    Friend Sub UpdateCheckByButton()
        Hint("该版本中不包含更新功能……")
    End Sub

    Friend IsUpdateWaitingRestart As Boolean = False
    Public Sub UpdateRestart(TriggerRestartAndByEnd As Boolean)
    End Sub
    Public Sub UpdateReplace(ProcessId As Integer, OldFileName As String, NewFileName As String, TriggerRestart As Boolean)
    End Sub

    ''' <summary>
    ''' 确保 PathTemp/Latest.exe 是最新正式版的 PCL，它会被用于整合包打包。
    ''' 如果不是，则下载一个。
    ''' </summary>
    Friend Sub DownloadLatestPCL(Optional LoaderToSyncProgress As LoaderBase = Nothing)
        '注意：如果要自行实现这个功能，请换用另一个文件路径，以免与官方版本冲突
    End Sub

#End Region

#Region "联网配置"

    ''' <summary>
    ''' 联网获取的配置信息。
    ''' 若获取失败或仍在获取中，可能为 Nothing。
    ''' </summary>
    Public ServerConfig As JObject

    Public ServerLoader As New LoaderTask(Of Integer, Integer)("PCL 配置更新", Sub() Logger.Info("该版本中不包含更新通知功能……"), Priority:=ThreadPriority.BelowNormal) With
        {.ReloadTimeout = 1000 * 60 * 60} '超时 1 小时

#End Region

#Region "赞助等级"

    Public ReadOnly Property CurrentRank As DonationRank
        Get
            Return DonationRank.None
        End Get
    End Property

    Public Sub InputPotatoCode(IsUpdating As Boolean)
    End Sub
    Friend Sub GeneratePotatoCode()
    End Sub

    ''' <summary>
    ''' 获取设备识别码。
    ''' </summary>
    Friend Function GetIdentify() As String
        Return "0000-0000-0000-0000"
    End Function

#End Region

End Module
