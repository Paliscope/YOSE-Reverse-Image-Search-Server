Imports MessagePack
<MessagePackObject> Public Class ImageRequest

    <Key(0)> Public Property Hashvalue As Byte()

    <Key(1)> Public Property frameindex As Integer

    <Key(2)> Public Property Image As Byte()

    <Key(3)> Public Property JCTHash As UShort()

    <Key(4)> Public Property Actions As List(Of Integer)

    <Key(5)> Public Property phash As ULong

    <Key(6)> Public Property VisualGroupID As Integer = -1



    Public Sub New()

    End Sub

End Class
