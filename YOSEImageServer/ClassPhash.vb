Imports Shipwreck.Phash
Imports Shipwreck.Phash.Bitmaps

Public Class ClassPhash

    Public Shared phashes As New List(Of Tuple(Of Integer, ULong))
    Public Const DefaultPhashThreashold As Integer = 8


    Public Shared Function FindGroup(pHash As ULong) As Integer

        Dim groupidid As Integer = -1
        If phashes.Count > 10000000 Then
            Parallel.For(0, phashes.Count,
               Sub(i As Integer)

                   If ImagePhash.GetHammingDistance(phashes(i).Item2, pHash) <= DefaultPhashThreashold Then
                       groupidid = phashes(i).Item1
                       Exit Sub
                   End If
               End Sub)
        Else
            For i As Integer = 0 To phashes.Count - 1
                If ImagePhash.GetHammingDistance(phashes(i).Item2, pHash) <= DefaultPhashThreashold Then
                    Return phashes(i).Item1
                End If
            Next


        End If

        Return groupidid


    End Function

    Public Shared Function FindGroup(bmp As Bitmap) As Integer

        Try
            Return FindGroup(ImagePhash.ComputeDctHash(bmp.ToLuminanceImage))

        Catch ex As Exception

        End Try

    End Function

    Public Shared Sub AddPhash(GroupID As Integer, phash As ULong)
        phashes.Add(New Tuple(Of Integer, ULong)(GroupID, phash))
    End Sub


End Class
