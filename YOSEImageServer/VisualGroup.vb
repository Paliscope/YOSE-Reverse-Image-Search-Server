
Imports MessagePack

<MessagePackObject> Public Class VisualGroup

    <Key(0)> Public Property VisualGroupID As Integer
    <Key(1)> Public Property phash As ULong
    <Key(2)> Public Property JCTHash As UShort()
    <Key(3)> Public Property hashvalues As New List(Of Byte())
    <Key(4)> Public Property Frameindexes As New List(Of Integer)


    Public Sub New()

    End Sub

End Class