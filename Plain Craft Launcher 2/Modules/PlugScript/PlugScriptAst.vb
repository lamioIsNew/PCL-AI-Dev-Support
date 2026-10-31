' PlugScript 的表达式与语句节点定义。
Option Strict Off

Imports System.Collections.Generic

Friend MustInherit Class PsExpr
    Public Property Line As Integer
End Class

Friend Class PsLiteralExpr
    Inherits PsExpr
    Public Property Value As Object
End Class

Friend Class PsIdentifierExpr
    Inherits PsExpr
    Public Property Name As String
End Class

Friend Class PsMemberExpr
    Inherits PsExpr
    Public Property Target As PsExpr
    Public Property MemberName As String
End Class

Friend Class PsIndexExpr
    Inherits PsExpr
    Public Property Target As PsExpr
    Public Property Index As PsExpr
End Class

Friend Class PsCallExpr
    Inherits PsExpr
    Public Property Callee As PsExpr
    Public Property Arguments As New List(Of PsExpr)()
End Class

Friend Class PsArrayExpr
    Inherits PsExpr
    Public Property Items As New List(Of PsExpr)()
End Class

Friend Class PsUnaryExpr
    Inherits PsExpr
    Public Property [Operator] As String
    Public Property Operand As PsExpr
End Class

Friend Class PsBinaryExpr
    Inherits PsExpr
    Public Property Left As PsExpr
    Public Property [Operator] As String
    Public Property Right As PsExpr
End Class

Friend MustInherit Class PsStmt
    Public Property Line As Integer
End Class

Friend Class PsLetStmt
    Inherits PsStmt
    Public Property Name As String
    Public Property Value As PsExpr
    Public Property IsConstant As Boolean
End Class

''' <summary>赋值目标，例如 a.b.c 会保存为 a、b、c 三段。</summary>
Friend Class PsTarget
    Public Property Segments As New List(Of String)()
End Class

Friend Class PsAssignStmt
    Inherits PsStmt
    Public Property Target As PsTarget
    Public Property Value As PsExpr
End Class

Friend Class PsExprStmt
    Inherits PsStmt
    Public Property Expression As PsExpr
End Class

Friend Class PsIfStmt
    Inherits PsStmt
    Public Property Condition As PsExpr
    Public Property ThenBody As New List(Of PsStmt)()
    Public Property ElseBody As New List(Of PsStmt)()
End Class

Friend Class PsWhileStmt
    Inherits PsStmt
    Public Property Condition As PsExpr
    Public Property Body As New List(Of PsStmt)()
End Class

Friend Class PsFunctionStmt
    Inherits PsStmt
    Public Property Name As String
    Public Property Parameters As New List(Of String)()
    Public Property Body As New List(Of PsStmt)()
End Class

Friend Class PsReturnStmt
    Inherits PsStmt
    Public Property Value As PsExpr
End Class

Friend Class PsProgram
    Public Property Statements As New List(Of PsStmt)()
    Public Property Functions As New Dictionary(Of String, PsFunctionStmt)(StringComparer.OrdinalIgnoreCase)
End Class
