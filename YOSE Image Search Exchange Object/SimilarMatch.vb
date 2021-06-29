Imports MessagePack

Public Class SimilarMatch
    Public Property GroupID As Integer
    Public Property Distance As Integer

    Public Sub New(GroupID As Integer, distance As Integer)
        Me.GroupID = GroupID
        Me.Distance = distance
    End Sub

End Class


