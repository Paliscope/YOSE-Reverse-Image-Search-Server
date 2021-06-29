Imports System.Drawing.Imaging
Imports System.IO
Imports System.Numerics
Imports ImageDatabase.Helper.Tree
Imports Shipwreck.Phash
Imports Shipwreck.Phash.Bitmaps
Imports YOSE_Image_Search_Exchange_Object

Public Class ClassImageDatabase

    'Default values for matching similarity // visual similar
    Public Const DefaultThreashold As Integer = 22

    'Default distance for Phash match // visual copy
    Public Const DefaultPhashThreashold As Integer = 8

    'Tree structures for phash or JCT descriptors
    Dim tree As New ImageDatabase.Helper.Tree.BKTree(Of CEDDTreeNode)
    Dim treephash As New ImageDatabase.Helper.Tree.BKTree(Of phashTreeNode)  'use shipwreck to compare them


    ''' <summary>
    ''' Clear both vector trees
    ''' </summary>
    Public Sub ClearTrees()
        tree = Nothing
        tree = New ImageDatabase.Helper.Tree.BKTree(Of CEDDTreeNode)

        treephash = Nothing
        treephash = New ImageDatabase.Helper.Tree.BKTree(Of phashTreeNode)  'use shipwreck to compare them
    End Sub

    ''' <summary>
    ''' Add JCT vector to tree
    ''' </summary>
    ''' <param name="hashvalue"></param>
    ''' <param name="frameindex"></param>
    ''' <param name="array"></param>
    Public Sub AddVectorToSearchTree(GroupID As Integer, array As UShort())

        Dim n As New CEDDTreeNode()
        n.JCTDescriptor = array
        n.GroupID = GroupID

        For i As Integer = 0 To 167
            n.Sum += array(i)
        Next
        tree.add(n)

    End Sub



    ''' <summary>
    ''' Add Phash number to tree
    ''' </summary>
    ''' <param name="hashvalue"></param>
    ''' <param name="frameindex"></param>
    ''' <param name="phash"></param>
    Public Sub AddpHashToSearchTree(groupid As Integer, phash As ULong)

        Dim n As New phashTreeNode()
        n.phash = phash
        n.GroupID = groupid


        treephash.add(n)

    End Sub


    ''' <summary>
    ''' Add JCT vector
    ''' </summary>
    ''' <param name="hashvalue"></param>
    ''' <param name="frameindex"></param>
    ''' <param name="array"></param>
    Public Sub AddVectorToSearchTree(Groupid As Integer, array As Double())

        Dim n As New CEDDTreeNode()
        n.JCTDescriptor = DoubleToUShortArray(array)
        n.GroupID = Groupid
        tree.add(n)

    End Sub


    ''' <summary>
    ''' Query an image as byte array()
    ''' </summary>
    ''' <param name="imgdata"></param>
    ''' <param name="Threashold"></param>
    ''' <returns></returns>
    Public Function QueryImage(imgdata As Byte(), Optional Threashold As Integer = DefaultThreashold) As List(Of SimilarMatch)

        Try
            Using str As New MemoryStream(imgdata)
                Using bmp As New Bitmap(str)

                    Return Query(DoubleToUShortArray(CalculateVector(bmp)), Threashold)
                End Using
            End Using

        Catch ex As Exception
            ' MsgBox("Querying Imgdata " & ex.Message)
        End Try


    End Function

    ''' <summary>
    ''' Query an image as bitmap
    ''' </summary>
    ''' <param name="bmp"></param>
    ''' <param name="Threashold"></param>
    ''' <returns></returns>
    Public Function Query(bmp As Bitmap, Optional Threashold As Integer = DefaultThreashold) As List(Of SimilarMatch)

        Return Query(DoubleToUShortArray(CalculateVector(bmp)), Threashold)

    End Function

    ''' <summary>
    ''' Query an image for a Phash
    ''' </summary>
    ''' <param name="bmp"></param>
    ''' <param name="Threashold"></param>
    ''' <returns></returns>
    Public Function QuerypHash(bmp As Bitmap, Optional Threashold As Integer = DefaultPhashThreashold) As List(Of SimilarMatch)

        Return QueryPhash(ImagePhash.ComputeDctHash(bmp.ToLuminanceImage), Threashold)

    End Function

    ''' <summary>
    ''' Query a Phash
    ''' </summary>
    ''' <param name="phash"></param>
    ''' <param name="Threashold"></param>
    ''' <returns></returns>
    Public Function QueryPhash(phash As ULong, Optional Threashold As Integer = DefaultPhashThreashold) As List(Of SimilarMatch)

        Dim n As New phashTreeNode()
        n.phash = phash
        Try

            Dim res As Dictionary(Of phashTreeNode, Integer) = treephash.query(n, Threashold)

            Dim result As New List(Of SimilarMatch)
            For Each keyval As KeyValuePair(Of phashTreeNode, Integer) In res
                result.Add(New SimilarMatch(keyval.Key.GroupID, keyval.Value))
            Next

            res.Clear()

            result.Sort(New SimilarMatchComparer)
            Return result
        Catch ex As Exception
            Return Nothing
        End Try

    End Function


    Public Function QueryPhashGetFirstMatchVisualGroup(phash As ULong, Optional Threashold As Integer = DefaultPhashThreashold) As Integer

        Dim n As New phashTreeNode()
        n.phash = phash
        Try

            Dim res As Dictionary(Of phashTreeNode, Integer) = treephash.querygetfirst(n, Threashold)


            If res.Count > 0 Then
                Return res.First.Key.GroupID
            Else
                Return -1
            End If

        Catch ex As Exception
            Return Nothing
        End Try

    End Function

    ''' <summary>
    ''' Convert Byte array to HEX hashvalue
    ''' </summary>
    ''' <param name="sha1hash"></param>
    ''' <returns></returns>
    Public ReadOnly Property HashvalueHex(sha1hash As Byte()) As String

        Get

            Try
                Return BitConverter.ToString(sha1hash).Replace("-", "")
            Catch ex As Exception

            End Try

        End Get

    End Property


    ''' <summary>
    ''' Query a Phash value
    ''' </summary>
    ''' <param name="phash"></param>
    ''' <param name="Threashold"></param>
    ''' <returns></returns>
    Public Function QueryPhashCount(phash As ULong, Optional Threashold As Integer = DefaultPhashThreashold) As Integer

        Dim n As New phashTreeNode()
        n.phash = phash

        Dim res As Dictionary(Of phashTreeNode, Integer) = treephash.query(n, Threashold)

        Return res.Count

    End Function

    ''' <summary>
    ''' Query a JCT vector
    ''' </summary>
    ''' <param name="array"></param>
    ''' <param name="Threashold"></param>
    ''' <returns></returns>
    Public Function Query(array As UShort(), Optional Threashold As Integer = DefaultThreashold) As List(Of SimilarMatch)

        Dim n As New CEDDTreeNode()
        n.JCTDescriptor = array
        For i As Integer = 0 To 167
            n.Sum += array(i)
        Next
        Dim result As New List(Of SimilarMatch)

        Try
            'MsgBox("Array length " & array.Length)
            Dim res As Dictionary(Of CEDDTreeNode, Integer) = tree.query(n, Threashold)
            ' MsgBox("2 Search")

            For Each keyval As KeyValuePair(Of CEDDTreeNode, Integer) In res
                result.Add(New SimilarMatch(keyval.Key.GroupID, keyval.Value))
            Next

            res.Clear()

        Catch ex As Exception
            'MsgBox("Query Imgdata " & ex.Message)
        End Try


        result.Sort(New SimilarMatchComparer)
        Return result

    End Function

    ''' <summary>
    ''' Convert Double array to Ushort
    ''' </summary>
    ''' <param name="arr"></param>
    ''' <returns></returns>
    Public Function DoubleToUShortArray(arr As Double()) As UShort()

        Dim res(167) As UShort

        For i As Integer = 0 To 167
            res(i) = UShort.MaxValue * arr(i)
        Next

        Return res

    End Function


    ''' <summary>
    ''' Calculate the JCT vector for an image
    ''' </summary>
    ''' <param name="bmp"></param>
    ''' <returns></returns>
    Public Function CalculateVector(bmp As Bitmap) As Double()


        Try

            Dim cedd As New CEDD_Descriptor.CEDD
            Dim FCTH As New FCTH_Descriptor.FCTH

            If bmp.PixelFormat <> PixelFormat.Format24bppRgb Then

                Using bmp2 As Bitmap = bmp.Clone(New Rectangle(0, 0, bmp.Width, bmp.Height), PixelFormat.Format24bppRgb)
                    Dim Ceddres As Double() = cedd.Apply(bmp2)
                    Dim FCTHres As Double() = FCTH.Apply(bmp2, 2)
                    Return FCTH.JointHistograms(Ceddres, FCTHres)

                End Using

            Else
                Dim Ceddres As Double() = cedd.Apply(bmp)
                Dim FCTHres As Double() = FCTH.Apply(bmp, 2)
                Return FCTH.JointHistograms(Ceddres, FCTHres)
            End If

        Catch ex As Exception
            ' MsgBox("error Calculate " & ex.Message)
        End Try


    End Function


    ''' <summary>
    ''' Calculate the JCT vector for an image as byte array
    ''' </summary>
    ''' <param name="imgdata"></param>
    ''' <returns></returns>
    Public Function CalculateVector(imgdata As Byte()) As Double()

        Using str As New MemoryStream(imgdata)
            Using bmp As New Bitmap(str)
                Return CalculateVector(bmp)
            End Using
        End Using

    End Function


End Class





