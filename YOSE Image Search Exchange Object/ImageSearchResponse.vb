
Imports MessagePack

<MessagePackObject> Public Class ImageSearchResponse



    'Search object, return a reference to the file
    <Key(0)> Public Property Hashvalue As Byte()



    'Visual Copies result from Phash

    <Key(1)> Public Property VisualCopies_Hashvalue As List(Of String)

    <Key(2)> Public Property VisualCopies_FrameIndex As List(Of Integer)

    <Key(3)> Public Property VisualCopies_Distance As List(Of Integer)




    'Visual Similar Result from Similar images

    <Key(4)> Public Property VisualSimilar_Hashvalue As List(Of String)

    <Key(5)> Public Property VisualSimilar_Frameindex As List(Of Integer)

    <Key(6)> Public Property VisualSimilar_Distance As List(Of Integer)


End Class

