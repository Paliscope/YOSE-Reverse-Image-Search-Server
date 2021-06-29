Imports MessagePack

<MessagePackObject> Public Class ImageVectorObject2



    <Key(0)> Public Property VisualGroupID As Integer

    <Key(1)> Public Property MD5hashvalue As Byte()

    <Key(2)> Public Property frameindex As Integer

    <Key(3)> Public Property phash As ULong

    <Key(4)> Public Property JCTHash As UShort()

    <Key(5)> Public Property Thumbnail As Byte()

    <Key(6)> Public Property Classification As Integer

    <Key(7)> Public Property Metadata As String



    Public Sub New()

    End Sub


End Class


