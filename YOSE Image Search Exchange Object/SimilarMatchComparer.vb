Public Class SimilarMatchComparer

    Implements IComparer(Of SimilarMatch)

    Public Sub New()


    End Sub




    Public Function Compare(x As SimilarMatch, y As SimilarMatch) As Integer Implements IComparer(Of SimilarMatch).Compare

        If x.Distance > y.Distance Then
            Return 1

        ElseIf x.Distance = y.Distance Then
            Return 0

        Else
            Return -1
        End If

    End Function


End Class
