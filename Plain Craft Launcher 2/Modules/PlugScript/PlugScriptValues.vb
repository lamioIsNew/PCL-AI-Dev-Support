' PlugScript 的值模型与通用辅助方法。
' 这里只依赖 .NET 基础类，不引用任何 WPF 类型。
Option Strict Off

Imports System.Collections
Imports System.Collections.Generic
Imports System.Globalization
Imports IDictionary = System.Collections.IDictionary
Imports IList = System.Collections.IList
Imports ICollection = System.Collections.ICollection

''' <summary>
''' 带源码行号的 PlugScript 异常。引擎会在 Execute 中统一转换为错误结果。
''' </summary>
Friend Class PsScriptException
    Inherits Exception

    ''' <summary>出错语句的源码行号（从 1 开始，0 表示未知）。</summary>
    Public ReadOnly Property Line As Integer

    Public Sub New(Message As String, Line As Integer)
        MyBase.New(Message)
        Me.Line = Line
    End Sub
End Class

''' <summary>由引擎直接实现的函数，例如 print、Storage.read。</summary>
Friend Class PsNativeFunction
    Public ReadOnly Property Name As String
    Public ReadOnly Property Handler As Func(Of Object(), Object)

    Public Sub New(Name As String, Handler As Func(Of Object(), Object))
        Me.Name = Name
        Me.Handler = Handler
    End Sub
End Class

''' <summary>脚本中通过 function 声明的函数，在求值时惰性创建。</summary>
Friend Class PsUserFunction
    Public ReadOnly Property Name As String

    Public Sub New(Name As String)
        Me.Name = Name
    End Sub
End Class

''' <summary>值操作辅助方法。</summary>
Friend Module PsValues

    ''' <summary>创建一个忽略大小写的字符串字典，用于 UI、Plugin 与 island 对象。</summary>
    Public Function NewMap() As Dictionary(Of String, Object)
        Return New Dictionary(Of String, Object)(StringComparer.OrdinalIgnoreCase)
    End Function

    Public Function IsNumeric(Value As Object) As Boolean
        If Value Is Nothing Then Return False
        Return TypeOf Value Is Byte OrElse TypeOf Value Is SByte OrElse
               TypeOf Value Is Short OrElse TypeOf Value Is UShort OrElse
               TypeOf Value Is Integer OrElse TypeOf Value Is UInteger OrElse
               TypeOf Value Is Long OrElse TypeOf Value Is ULong OrElse
               TypeOf Value Is Single OrElse TypeOf Value Is Double OrElse
               TypeOf Value Is Decimal
    End Function

    Public Function ToNumber(Value As Object, Line As Integer) As Double
        If IsNumeric(Value) Then Return Convert.ToDouble(Value, CultureInfo.InvariantCulture)
        If TypeOf Value Is String Then
            Dim Parsed As Double
            If Double.TryParse(CStr(Value), NumberStyles.Float, CultureInfo.InvariantCulture, Parsed) Then Return Parsed
        End If
        If Value Is Nothing Then Return 0.0R
        Throw New PsScriptException($"无法将值「{ToText(Value)}」作为数字使用。", Line)
    End Function

    ''' <summary>JavaScript 风格的「真值」判断：空字符串、0、Nothing、False 为假。</summary>
    Public Function IsTruthy(Value As Object) As Boolean
        If Value Is Nothing Then Return False
        If TypeOf Value Is Boolean Then Return CBool(Value)
        If IsNumeric(Value) Then Return Convert.ToDouble(Value, CultureInfo.InvariantCulture) <> 0.0R
        If TypeOf Value Is String Then Return CStr(Value).Length > 0
        If TypeOf Value Is ICollection Then Return DirectCast(Value, ICollection).Count > 0
        Return True
    End Function

    ''' <summary>面向用户展示的字符串形式。</summary>
    Public Function ToText(Value As Object) As String
        If Value Is Nothing Then Return ""
        If TypeOf Value Is Boolean Then Return If(CBool(Value), "True", "False")
        If TypeOf Value Is Double Then
            Dim Number = CDbl(Value)
            If Not Double.IsNaN(Number) AndAlso Not Double.IsInfinity(Number) AndAlso Number = Math.Truncate(Number) Then
                Return Number.ToString("0", CultureInfo.InvariantCulture)
            End If
            Return Number.ToString("R", CultureInfo.InvariantCulture)
        End If
        If TypeOf Value Is Single Then
            Dim Number = CSng(Value)
            If Not Single.IsNaN(Number) AndAlso Not Single.IsInfinity(Number) AndAlso Number = Math.Truncate(Number) Then
                Return Number.ToString("0", CultureInfo.InvariantCulture)
            End If
            Return Number.ToString("R", CultureInfo.InvariantCulture)
        End If
        If TypeOf Value Is IList Then
            Dim Parts As New List(Of String)()
            For Each Item In DirectCast(Value, IList)
                Parts.Add(If(TypeOf Item Is String, """" & ToText(Item) & """", ToText(Item)))
            Next
            Return "[" & String.Join(", ", Parts) & "]"
        End If
        If TypeOf Value Is IDictionary Then
            Return "{...}"
        End If
        Return Convert.ToString(Value, CultureInfo.InvariantCulture)
    End Function

    Public Function ValueEquals(Left As Object, Right As Object) As Boolean
        If Left Is Nothing AndAlso Right Is Nothing Then Return True
        If Left Is Nothing OrElse Right Is Nothing Then Return False
        If IsNumeric(Left) AndAlso IsNumeric(Right) Then
            Return Convert.ToDouble(Left, CultureInfo.InvariantCulture) = Convert.ToDouble(Right, CultureInfo.InvariantCulture)
        End If
        If TypeOf Left Is String AndAlso TypeOf Right Is String Then
            Return String.Equals(CStr(Left), CStr(Right), StringComparison.Ordinal)
        End If
        If TypeOf Left Is Boolean AndAlso TypeOf Right Is Boolean Then Return CBool(Left) = CBool(Right)
        Return Object.Equals(Left, Right)
    End Function

    ''' <summary>读取成员：字典、列表/字符串的 length，支持大小写不敏感回退。</summary>
    Public Function TryGetMember(Target As Object, Name As String, ByRef Value As Object) As Boolean
        If Target Is Nothing Then Return False

        If TypeOf Target Is IDictionary Then
            Dim Map = DirectCast(Target, IDictionary)
            If Map.Contains(Name) Then
                Value = Map(Name)
                Return True
            End If
            For Each Key In Map.Keys
                If String.Equals(Convert.ToString(Key, CultureInfo.InvariantCulture), Name, StringComparison.OrdinalIgnoreCase) Then
                    Value = Map(Key)
                    Return True
                End If
            Next
            Value = Nothing
            Return False
        End If

        If TypeOf Target Is IList Then
            Dim List = DirectCast(Target, IList)
            If String.Equals(Name, "length", StringComparison.OrdinalIgnoreCase) OrElse
               String.Equals(Name, "count", StringComparison.OrdinalIgnoreCase) Then
                Value = List.Count
                Return True
            End If
        End If

        If TypeOf Target Is String Then
            If String.Equals(Name, "length", StringComparison.OrdinalIgnoreCase) Then
                Value = CStr(Target).Length
                Return True
            End If
        End If

        Return False
    End Function

    Public Function SetMember(Target As Object, Name As String, Value As Object, Line As Integer) As Boolean
        If TypeOf Target Is IDictionary Then
            DirectCast(Target, IDictionary)(Name) = Value
            Return True
        End If
        Throw New PsScriptException($"无法给「{ToText(Target)}」的成员 {Name} 赋值，目标不是对象。", Line)
    End Function

    Public Function GetIndex(Target As Object, Index As Object, Line As Integer) As Object
        If TypeOf Target Is IList Then
            Dim List = DirectCast(Target, IList)
            Dim Position As Integer
            If IsNumeric(Index) Then
                Position = CInt(Math.Truncate(ToNumber(Index, Line)))
            ElseIf TypeOf Index Is String AndAlso Integer.TryParse(CStr(Index), Position) Then
                ' 允许 a["0"] 这种写法
            Else
                Throw New PsScriptException("列表下标必须是数字。", Line)
            End If
            If Position < 0 OrElse Position >= List.Count Then
                Throw New PsScriptException($"列表下标 {Position} 越界（长度 {List.Count}）。", Line)
            End If
            Return List(Position)
        End If

        If TypeOf Target Is String Then
            Dim Text = CStr(Target)
            Dim Position = CInt(Math.Truncate(ToNumber(Index, Line)))
            If Position < 0 OrElse Position >= Text.Length Then
                Throw New PsScriptException($"字符串下标 {Position} 越界（长度 {Text.Length}）。", Line)
            End If
            Return Text.Substring(Position, 1)
        End If

        If TypeOf Target Is IDictionary Then
            Dim Key = ToText(Index)
            Dim Value As Object = Nothing
            If TryGetMember(Target, Key, Value) Then Return Value
            Return Nothing
        End If

        Throw New PsScriptException("只能对列表、字符串或对象使用下标。", Line)
    End Function

    Public Function DeepMerge(Target As IDictionary, Source As Object) As Boolean
        If Not (TypeOf Source Is IDictionary) Then Return False
        For Each Key In DirectCast(Source, IDictionary).Keys
            Target(Key) = DirectCast(Source, IDictionary)(Key)
        Next
        Return True
    End Function

End Module
