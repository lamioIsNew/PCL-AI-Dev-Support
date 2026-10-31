' PlugScript 的宿主上下文与执行结果。
Option Strict Off

Imports System.Collections.Generic

''' <summary>
''' PlugScript 的运行上下文。宿主可预填 Ui / Plugin，并注入存储与权限委托。
''' </summary>
Public Class PlugScriptContext

    Private _pluginName As String = ""
    Private _pluginFolder As String = ""

    ''' <summary>插件名称。设置时会同步到 Plugin("Name")。</summary>
    Public Property PluginName As String
        Get
            Return _pluginName
        End Get
        Set(Value As String)
            _pluginName = If(Value, "")
            Plugin("Name") = _pluginName
        End Set
    End Property

    ''' <summary>插件目录。设置时会同步到 Plugin("Path")。</summary>
    Public Property PluginFolder As String
        Get
            Return _pluginFolder
        End Get
        Set(Value As String)
            _pluginFolder = If(Value, "")
            Plugin("Path") = _pluginFolder
        End Set
    End Property

    ''' <summary>全局变量表，键名大小写不敏感。</summary>
    Public ReadOnly Property Variables As New Dictionary(Of String, Object)(StringComparer.OrdinalIgnoreCase)

    ''' <summary>插件 UI 配置，键名大小写不敏感。</summary>
    Public ReadOnly Property Ui As New Dictionary(Of String, Object)(StringComparer.OrdinalIgnoreCase)

    ''' <summary>插件元信息，键名大小写不敏感。</summary>
    Public ReadOnly Property Plugin As New Dictionary(Of String, Object)(StringComparer.OrdinalIgnoreCase)

    ''' <summary>由 print / log 产生的日志。</summary>
    Public ReadOnly Property Logs As New List(Of String)()

    ''' <summary>单个存储文件允许的最大字节数。0 表示不限制。</summary>
    Public Property MaxStorageBytes As Long = 10L * 1024L * 1024L

    Public Property StorageRead As Func(Of String, String)
    Public Property StorageWrite As Action(Of String, String)
    Public Property StorageExists As Func(Of String, Boolean)
    Public Property StorageDelete As Action(Of String)
    Public Property StorageList As Func(Of String())
    Public Property PermissionCheck As Func(Of String, Boolean)

    Public Sub New()
        ' 预置最小默认值，宿主之后可以直接覆盖。
        Ui("type") = "window"
        Ui("FileSize") = 0L
        Ui("title") = ""
        Ui("width") = 800L
        Ui("height") = 600L

        Plugin("Name") = ""
        Plugin("Version") = ""
        Plugin("AccessRequest") = Nothing
        Plugin("Path") = ""
    End Sub

    ''' <summary>追加一条日志。</summary>
    Public Sub Log(Message As String)
        Logs.Add(If(Message, ""))
    End Sub

    ''' <summary>检查权限；未配置权限委托时返回 False。</summary>
    Public Function HasPermission(Name As String) As Boolean
        If PermissionCheck Is Nothing Then Return False
        Return PermissionCheck.Invoke(Name)
    End Function

End Class

''' <summary>一次 PlugScript 执行的返回值。</summary>
Public Class PlugScriptResult

    Public Property Success As Boolean
    Public Property ErrorMessage As String
    Public Property ErrorLine As Integer
    Public Property Context As PlugScriptContext

    ''' <summary>入口函数（或 ExecuteFunction 指定函数）的返回值。</summary>
    Public Property Result As Object

End Class
