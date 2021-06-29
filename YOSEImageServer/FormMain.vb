Imports System.IO
Imports System.Threading
Imports System.Timers
Imports CEDD_Descriptor
Imports Clifton.Core.Pipes
Imports Shipwreck.Phash
Imports Shipwreck.Phash.Bitmaps
Imports System.Drawing.Imaging
Imports MessagePack
Imports System.Runtime.ExceptionServices
Imports System.Security.Cryptography
Imports YOSE_Image_Search_Exchange_Object

Public Class FormMain


    'Pipelines to communicate with server. Use 2 different ones since
    'the calling application has different threads using different functions (adding and query)
    Dim ServerPipeAdd As ServerPipe
    Dim ServerPipeQuery As ServerPipe
    Dim clientPipe As ClientPipe

    'Debug flag for... debugging
    Dim isdebug As Boolean = False

    'Keep a live timer so the process don't get down prioritized when hidded. Not sure if this is needed or not. But it is there. Feels good.
    WithEvents KeepAliveTimer As New Timers.Timer()


    'Database Access for all vectors
    Public Const DatabaseName_Files As String = "YOSE ImageVectors.RocksDB"
    Public Const DatabaseName_VisualGroups As String = "YOSE VisualGroups.RocksDB"
    Dim DataBasePath As String

    Dim db_Files As RocksDbSharp.RocksDb
    Dim db_VisualGroups As RocksDbSharp.RocksDb


    'The image hash database trees
    Public VisuallySimilarImageSignaturesDatabase As New ClassImageDatabase()


    'Syncing writing and reading to the trees
    Dim SyncObjLockImageSignaturesDatabase As New ReaderWriterLock()
    Dim SyncObjLockPhashsDatabase As New ReaderWriterLock()

    Public MaxVisualGroupID As Integer = 0

    Public Handler As New AutoResetEvent(False)
    Public HandlerSimilar As New AutoResetEvent(False)

    Public Closing As Boolean = False

    Dim AddImageQueu As New Concurrent.ConcurrentQueue(Of Byte())


    Private CheckIfFileExisitsObject As New Object

    Private Sub FormMain_Load(sender As Object, e As EventArgs) Handles MyBase.Load

        Try

            If System.IO.File.Exists("C:\ProgramData\Paliscope Yose\debug.txt") Then
                isdebug = True
                Me.ShowInTaskbar = True
            Else
                Me.Hide()
                Me.ShowInTaskbar = False

            End If
        Catch ex As Exception
            Me.Hide()
            Me.ShowInTaskbar = False
        End Try

        Try


            'Get the path to the database

            'It can start as an image server or as a process engine to create visual hashes
            If My.Application.CommandLineArgs(0) = "server" Then
                DataBasePath = My.Application.CommandLineArgs(1)

                'Creating communication pipelines to communicate with the image server
                ServerPipeAdd = New ServerPipe("YOSEImageAdd", Sub(p) p.StartByteReaderAsync())
                AddHandler ServerPipeAdd.DataReceived, AddressOf DataReceivedAdd
                AddHandler ServerPipeAdd.Connected, AddressOf ServertPipeAddConnectedHandler


                ServerPipeQuery = New ServerPipe("YOSEImageQuery", Sub(p) p.StartByteReaderAsync())
                AddHandler ServerPipeQuery.DataReceived, AddressOf DataReceivedQuery
                AddHandler ServerPipeQuery.Connected, AddressOf ServerPipeQueryConnectedHandler
                If isdebug Then Me.TextBox1.AppendText("Image Database Server started ok" & vbCrLf)
            Else
                Try
                    clientPipe = New ClientPipe(".", "YOSEIMAGEPROCESS" & My.Application.CommandLineArgs(1), Sub(p) p.StartByteReaderAsync())
                    AddHandler clientPipe.DataReceived, AddressOf DataReceivedCreateVector
                    clientPipe.Connect()
                    If isdebug Then Me.TextBox1.AppendText("Image Processing Unit started ok" & vbCrLf)
                Catch ex As Exception

                End Try
            End If


            'Keep alive timer
            KeepAliveTimer.Interval = 5 * 60 * 1000
            KeepAliveTimer.Start()




            'Start the image server if it is a server and not a processing node
            If Not IsNothing(ServerPipeAdd) Then

                Task.Factory.StartNew(Sub() InsertProcess(), TaskCreationOptions.LongRunning)

                Task.Run(Sub() LoadDatabase())

            End If



        Catch ex As Exception
            If isdebug Then Me.TextBox1.AppendText("Error " & ex.Message)
        End Try

    End Sub

    Private Sub DataReceivedCreateVector(sender As Object, e As PipeEventArgs)

        Try
            Task.Run(Sub() ProcessImage(e.Data))
        Catch ex As Exception

        End Try


    End Sub


    <HandleProcessCorruptedStateExceptions>
    Public Sub ProcessImage(data As Byte())


        Dim QObj As New ImageRequest
        Try



            'Vector for similar images
            Dim JCTHash() As Double


            Using ms As New MemoryStream(data)

                Using bmp As New Bitmap(ms)


                    'Calculate the phash
                    QObj.phash = ImagePhash.ComputeDctHash(bmp.ToLuminanceImage)


                    'Calculate the JCTHash
                    Dim cedd As New CEDD
                    Dim FCTH As New FCTH_Descriptor.FCTH

                    'Always convert to 24bits pixelformat to avoid issues
                    If bmp.PixelFormat <> PixelFormat.Format24bppRgb Then

                        Using bmp2 As Bitmap = bmp.Clone(New Rectangle(0, 0, bmp.Width, bmp.Height), PixelFormat.Format24bppRgb)
                            Dim Ceddres As Double() = cedd.Apply(bmp2)
                            Dim FCTHres As Double() = FCTH.Apply(bmp2, 2)
                            JCTHash = FCTH.JointHistograms(Ceddres, FCTHres)

                        End Using

                    Else
                        Dim Ceddres As Double() = cedd.Apply(bmp)
                        Dim FCTHres As Double() = FCTH.Apply(bmp, 2)
                        JCTHash = FCTH.JointHistograms(Ceddres, FCTHres)
                    End If


                End Using

            End Using

            '
            'Add to database and tree structures

            ReDim QObj.JCTHash(167)
            'Converting to Ushort to save memory. Losing a little precision but winning RAM.
            For i As Integer = 0 To 167
                QObj.JCTHash(i) = UShort.MaxValue * JCTHash(i)
            Next
            JCTHash = Nothing
            QObj.Image = Nothing



        Catch ex2 As AccessViolationException
            'kill the process if it is access violation. The exe will be restarted by YOSE
            Process.GetCurrentProcess().Kill()


        Catch ex As Exception
            If isdebug Then Me.Invoke(Sub() Me.TextBox1.AppendText(ex.Message))

        Finally
            'Send back
            clientPipe.WriteBytes(MessagePackSerializer.Serialize(Of ImageRequest)(QObj))
        End Try

    End Sub

    Private Sub ServerPipeQueryConnectedHandler(sender As Object, e As EventArgs)

        'Just for debugging purposes
        Try
            If isdebug Then Me.Invoke(Sub() Me.TextBox1.AppendText("ServerPipeQueryConnectedHandler" & vbCrLf))
        Catch ex As Exception

        End Try


    End Sub

    Private Sub ServertPipeAddConnectedHandler(sender As Object, e As EventArgs)

        'Just for debugging purposes
        Try
            If isdebug Then Me.Invoke(Sub() Me.TextBox1.AppendText("ServerPipeAddConnectedHandler" & vbCrLf))
        Catch ex As Exception

        End Try


    End Sub


    Private Function FileExists(hashvalue As String, frameindex As Integer) As Boolean

        Try

            SyncLock (CheckIfFileExisitsObject)


                Dim hash As Byte() = CEDD.StringToByteArrayFastest(hashvalue)

                Dim frameindexbyte As Byte() = BitConverter.GetBytes(frameindex)
                hash = hash.Concat(frameindexbyte).ToArray


                Dim obj As Byte() = db_Files.Get(hash)


                If IsNothing(obj) Then
                    Return False
                Else
                    Return True
                End If

            End SyncLock


        Catch ex As Exception
            If isdebug Then Me.Invoke(Sub() Me.TextBox1.AppendText("FileExists: " & ex.Message & vbCrLf))

        End Try

    End Function


    Private Sub KeepAliveTimer_Elapsed(sender As Object, e As ElapsedEventArgs) Handles KeepAliveTimer.Elapsed

        Me.Invoke(Sub() Me.Refresh())

    End Sub


    Private Sub DataReceivedAdd(sender As Object, e As PipeEventArgs)

        Try
            AddImageQueu.Enqueue(e.Data)
            Handler.Set()

        Catch ex As Exception

        End Try


    End Sub



    Private Sub InsertProcess()


        Dim imgdata As Byte()
        Dim counter As Integer = 0
        While 1

            Try

                If Closing Then Exit Sub




                If AddImageQueu.Count = 0 Then
                    Handler.WaitOne()
                End If


                If Closing Then
                    Exit Sub
                End If

                If isdebug Then
                    If counter Mod 10 = 0 Then
                        Me.Invoke(Sub() Me.Text = AddImageQueu.Count)
                    End If

                End If
                If AddImageQueu.TryDequeue(imgdata) = True Then
                    AddImageToDatabase(imgdata)

                End If

                counter += 1

            Catch ex As Exception

            End Try

        End While

    End Sub

    Public Sub AddImageToDatabase(data As Byte())

        Try
            Dim QObj As ImageRequest = MessagePackSerializer.Deserialize(Of ImageRequest)(data)


            Dim groupid As Integer = ClassPhash.FindGroup(QObj.phash)


            If groupid > -1 Then
                AddToDatabase(QObj.Hashvalue, QObj.frameindex, groupid, QObj.phash, QObj.JCTHash, False)
            Else
                MaxVisualGroupID += 1
                ClassPhash.AddPhash(MaxVisualGroupID, QObj.phash)

                AddToDatabase(QObj.Hashvalue, QObj.frameindex, MaxVisualGroupID, QObj.phash, QObj.JCTHash, True)
            End If

            QObj = Nothing


        Catch ex As Exception
            If isdebug Then Me.Invoke(Sub() Me.TextBox1.AppendText(ex.Message))

        Finally

        End Try

    End Sub



    Private Sub AddToDatabase(hashvalue As Byte(), frameindex As Integer, GroupID As Integer, phash As ULong, JCTHash As UShort(), newgroup As Boolean)

        Try

            If Not IsNothing(JCTHash) Then


                'Save into RocksDB Files
                Dim obj As New ImageVectorObject2
                obj.frameindex = frameindex
                obj.phash = phash
                obj.JCTHash = JCTHash
                obj.MD5hashvalue = hashvalue
                obj.VisualGroupID = GroupID

                Dim hashkey As Byte() = obj.MD5hashvalue
                Dim frameindexbyte As Byte() = BitConverter.GetBytes(frameindex)

                hashkey = hashkey.Concat(frameindexbyte).ToArray
                db_Files.Put(hashkey, MessagePackSerializer.Serialize(Of ImageVectorObject2)(obj))



                'Save into Visual Groups
                Dim VisualGr As VisualGroup
                If newgroup Then
                    VisualGr = New VisualGroup
                    VisualGr.Frameindexes.Add(frameindex)
                    VisualGr.hashvalues.Add(hashvalue)
                    VisualGr.VisualGroupID = GroupID
                    VisualGr.JCTHash = JCTHash
                    VisualGr.phash = phash

                    'Save
                    db_VisualGroups.Put(BitConverter.GetBytes(VisualGr.VisualGroupID), MessagePackSerializer.Serialize(Of VisualGroup)(VisualGr))

                    'Add to tree
                    VisuallySimilarImageSignaturesDatabase.AddVectorToSearchTree(VisualGr.VisualGroupID, VisualGr.JCTHash)

                Else
                    'Load and update the visual group with more files
                    VisualGr = MessagePackSerializer.Deserialize(Of VisualGroup)(db_VisualGroups.Get(BitConverter.GetBytes(GroupID)))
                    VisualGr.Frameindexes.Add(frameindex)
                    VisualGr.hashvalues.Add(hashvalue)
                    db_VisualGroups.Put(BitConverter.GetBytes(VisualGr.VisualGroupID), MessagePackSerializer.Serialize(Of VisualGroup)(VisualGr))
                End If


            End If

        Catch ex As Exception

            If isdebug Then Me.Invoke(Sub() Me.TextBox1.AppendText("AddToDatabase: " & ex.Message & " " & GroupID & vbCrLf))

        End Try


    End Sub

    Private Sub DataReceivedQuery(sender As Object, e As PipeEventArgs)

        Try

            If isdebug Then Me.Invoke(Sub() Me.TextBox1.AppendText("Received Image" & vbCrLf))
            Task.Factory.StartNew(Sub() Query(e.Data))

        Catch ex As Exception

        End Try


    End Sub


    Private Sub FindvaluesFromDatabase(ByRef qobj As ImageRequest)

        Try

            Dim hash As Byte() = qobj.Hashvalue

            Dim frameindex As Byte() = BitConverter.GetBytes(qobj.frameindex)
            hash = hash.Concat(frameindex).ToArray


            Dim obj As Byte() = db_Files.Get(hash)

            Dim Imgobj As ImageVectorObject2

            If Not IsNothing(obj) Then
                Imgobj = MessagePackSerializer.Deserialize(Of ImageVectorObject2)(obj)

                qobj.phash = Imgobj.phash
                qobj.JCTHash = Imgobj.JCTHash
                qobj.VisualGroupID = Imgobj.VisualGroupID
            End If


        Catch ex As Exception
            If isdebug Then Me.Invoke(Sub() Me.TextBox1.AppendText("Finding hashvalue Integer database: " & ex.Message))

        End Try

    End Sub


    Public Sub Query(data As Byte())

        Dim returnobj As New ImageSearchResponse
        Dim QObj As ImageRequest

        Dim visualgr As VisualGroup
        Dim res As List(Of SimilarMatch)

        Try

            'What are we asking about...
            QObj = MessagePackSerializer.Deserialize(Of ImageRequest)(data)

        Catch ex As Exception
            If isdebug Then Me.Invoke(Sub() Me.TextBox1.AppendText("Deserializing: " & ex.Message))
        End Try

        If Not IsNothing(QObj.Hashvalue) Then
            returnobj.Hashvalue = QObj.Hashvalue
        End If



        Dim watcher As New Stopwatch



        Try

            'Close
            If QObj.Actions.Contains(-1) Then
                Closing = True
                Handler.Set()
                HandlerSimilar.Set()

                'Close
                Me.Invoke(Sub() Me.Close())
                Exit Sub


                'Ping
            ElseIf QObj.Actions.Contains(-2) Then 'ping
                Exit Sub

            End If

        Catch ex As Exception

        End Try



        Try
            'Try to find the hashvalue in the database to avoid processing again
            If Not IsNothing(QObj.Hashvalue) Then
                FindvaluesFromDatabase(QObj)
            End If


            If isdebug Then
                watcher.Start()
            End If

            'Calculate Visual Copies using Phash
            Try
                If QObj.Actions.Contains(1) Then

                    If QObj.VisualGroupID > 0 Then    'It is indexed

                        'Load from visual group instead
                        returnobj.VisualCopies_Distance = New List(Of Integer)
                        returnobj.VisualCopies_FrameIndex = New List(Of Integer)
                        returnobj.VisualCopies_Hashvalue = New List(Of String)

                        If isdebug Then
                            Me.Invoke(Sub() Me.TextBox1.AppendText("Found Visual groupQObj.VisualGroupID: " & QObj.VisualGroupID))
                        End If

                        visualgr = MessagePackSerializer.Deserialize(Of VisualGroup)(db_VisualGroups.Get(BitConverter.GetBytes(QObj.VisualGroupID)))

                        If isdebug Then
                            Me.Invoke(Sub() Me.TextBox1.AppendText("Found Visual groupQObj.VisualGroupID Done: " & visualgr.hashvalues.Count))
                        End If

                        For k As Integer = 0 To visualgr.hashvalues.Count - 1
                            returnobj.VisualCopies_Distance.Add(0)
                            returnobj.VisualCopies_Hashvalue.Add(HashvalueHex(visualgr.hashvalues(k)))
                            returnobj.VisualCopies_FrameIndex.Add(visualgr.Frameindexes(k))
                        Next

                    ElseIf Not IsNothing(QObj.phash) Then    'It is indexed

                        If isdebug Then
                            Me.Invoke(Sub() Me.TextBox1.AppendText("Got Phash: "))
                        End If

                        Dim GroupID As Integer = ClassPhash.FindGroup(QObj.phash)

                        returnobj.VisualCopies_Distance = New List(Of Integer)
                        returnobj.VisualCopies_FrameIndex = New List(Of Integer)
                        returnobj.VisualCopies_Hashvalue = New List(Of String)

                        If GroupID >= 0 Then

                            visualgr = MessagePackSerializer.Deserialize(Of VisualGroup)(db_VisualGroups.Get(BitConverter.GetBytes(GroupID)))
                            For k As Integer = 0 To visualgr.hashvalues.Count - 1
                                returnobj.VisualCopies_Distance.Add(0)
                                returnobj.VisualCopies_Hashvalue.Add(HashvalueHex(visualgr.hashvalues(k)))
                                returnobj.VisualCopies_FrameIndex.Add(visualgr.Frameindexes(k))
                            Next

                        End If

                    ElseIf Not IsNothing(QObj.Image) Then

                        If isdebug Then
                            Me.Invoke(Sub() Me.TextBox1.AppendText("Querying Image for visual copies"))
                        End If


                        Using ms As New MemoryStream(QObj.Image)
                            Using bmp As New Bitmap(ms)

                                Dim GrpID As Integer = ClassPhash.FindGroup(bmp)

                                If GrpID >= 0 Then

                                    visualgr = MessagePackSerializer.Deserialize(Of VisualGroup)(db_VisualGroups.Get(BitConverter.GetBytes(GrpID)))
                                    For k As Integer = 0 To visualgr.hashvalues.Count - 1
                                        returnobj.VisualCopies_Distance.Add(0)
                                        returnobj.VisualCopies_Hashvalue.Add(HashvalueHex(visualgr.hashvalues(k)))
                                        returnobj.VisualCopies_FrameIndex.Add(visualgr.Frameindexes(k))
                                    Next

                                End If

                            End Using
                        End Using


                    End If


                End If

            Catch ex As Exception
                If isdebug Then
                    Me.Invoke(Sub() Me.TextBox1.AppendText("Finding Visual Copies: " & ex.Message))
                End If
            End Try


            Try

                If isdebug Then
                    watcher.Stop()
                    Me.Invoke(Sub() Me.TextBox1.AppendText("Found Visual Copies: " & returnobj.VisualCopies_Hashvalue.Count & vbCrLf))
                    Me.Invoke(Sub() Me.TextBox1.AppendText("Elapsed time: " & watcher.ElapsedMilliseconds & vbCrLf))
                End If
            Catch ex As Exception
                If isdebug Then
                    Me.Invoke(Sub() Me.TextBox1.AppendText("Error When typing elapsed time: " & ex.Message & vbCrLf))
                End If
            End Try

            If isdebug Then
                watcher.Reset()
                watcher.Start()
            End If


            'Calculate Similar Images
            Try
                If QObj.Actions.Contains(2) Then


                    If Not IsNothing(QObj.JCTHash) Then
                        If isdebug Then Me.Invoke(Sub() Me.TextBox1.AppendText("Querying Image for vector"))
                        SyncObjLockImageSignaturesDatabase.AcquireReaderLock(10000)
                        res = VisuallySimilarImageSignaturesDatabase.Query(QObj.JCTHash)
                        SyncObjLockImageSignaturesDatabase.ReleaseReaderLock()

                    ElseIf Not IsNothing(QObj.Image) Then
                        If isdebug Then Me.Invoke(Sub() Me.TextBox1.AppendText("Querying Image for visual similar"))
                        SyncObjLockImageSignaturesDatabase.AcquireReaderLock(10000)
                        res = VisuallySimilarImageSignaturesDatabase.QueryImage(QObj.Image)
                        SyncObjLockImageSignaturesDatabase.ReleaseReaderLock()
                    End If
                End If

                If isdebug Then
                    If Not IsNothing(res) Then
                        Me.Invoke(Sub() Me.TextBox1.AppendText("Got Visually similar: " & res.Count))
                    End If

                End If

                returnobj.VisualSimilar_Distance = New List(Of Integer)
                returnobj.VisualSimilar_Frameindex = New List(Of Integer)
                returnobj.VisualSimilar_Hashvalue = New List(Of String)

                If Not IsNothing(res) Then

                    For i As Integer = 0 To res.Count - 1
                        visualgr = MessagePackSerializer.Deserialize(Of VisualGroup)(db_VisualGroups.Get(BitConverter.GetBytes(res(i).GroupID)))
                        For k As Integer = 0 To visualgr.hashvalues.Count - 1
                            returnobj.VisualSimilar_Distance.Add(res(i).Distance)
                            returnobj.VisualSimilar_Hashvalue.Add(HashvalueHex(visualgr.hashvalues(k)))
                            returnobj.VisualSimilar_Frameindex.Add(visualgr.Frameindexes(k))

                        Next
                    Next
                End If

            Catch ex As Exception
                If isdebug Then
                    Me.Invoke(Sub() Me.TextBox1.AppendText("Finding Visually similar: " & ex.Message))
                End If
            End Try


            If isdebug Then
                watcher.Stop()
                Me.Invoke(Sub() Me.TextBox1.AppendText("Found Visual Similar: " & returnobj.VisualSimilar_Hashvalue.Count & vbCrLf))
                Me.Invoke(Sub() Me.TextBox1.AppendText("Elapsed time: " & watcher.ElapsedMilliseconds & vbCrLf))

                watcher.Reset()
            End If

            'Send back the result


        Catch ex As AccessViolationException

            'kill the process if it is access violation. The calling application has to restart it
            Process.GetCurrentProcess().Kill()

        Catch ex As Exception
            If isdebug Then
                Me.Invoke(Sub() Me.TextBox1.AppendText(ex.Message))
            End If


        Finally

            ServerPipeQuery.WriteBytes(MessagePackSerializer.Serialize(Of ImageSearchResponse)(returnobj))

        End Try

    End Sub



    Private Sub LoadDatabase()

        Dim watcher As New Stopwatch

        'Open database
        'Get the database name
        Dim DBName_files As String = System.IO.Path.Combine(DataBasePath, DatabaseName_Files)
        Dim DBName_visualgroups As String = System.IO.Path.Combine(DataBasePath, DatabaseName_VisualGroups)

        If Not System.IO.File.Exists(System.IO.Path.Combine(DataBasePath, "Version.txt")) Then
            System.IO.File.WriteAllText(System.IO.Path.Combine(DataBasePath, "Version.txt"), "2")
        End If


        Dim options As New RocksDbSharp.DbOptions
        options.SetCreateIfMissing(True)
        options.SetKeepLogFileNum(10)
        db_Files = RocksDbSharp.RocksDb.Open(options, DBName_files)

        db_VisualGroups = RocksDbSharp.RocksDb.Open(options, DBName_visualgroups)

        Dim counter As Long = 0
        Dim counterfiles As Long = 0

        MaxVisualGroupID = 0

        watcher.Start()
        Try

            'Clear current data
            VisuallySimilarImageSignaturesDatabase.ClearTrees()
            SyncObjLockImageSignaturesDatabase.AcquireWriterLock(1000000)
            SyncObjLockPhashsDatabase.AcquireWriterLock(1000000)


            ClassPhash.phashes.Clear()

            Dim obj As VisualGroup
            Dim dbIterator As RocksDbSharp.Iterator = db_VisualGroups.NewIterator()
            dbIterator.SeekToFirst()

            While dbIterator.Valid

                Try

                    obj = MessagePackSerializer.Deserialize(Of VisualGroup)(dbIterator.Value)

                    VisuallySimilarImageSignaturesDatabase.AddVectorToSearchTree(obj.VisualGroupID, obj.JCTHash)
                    '                    VisualCopiesImageSignaturesDatabase.AddpHashToSearchTree(obj.VisualGroupID, obj.phash)

                    MaxVisualGroupID = Math.Max(MaxVisualGroupID, obj.VisualGroupID)
                    ClassPhash.phashes.Add(New Tuple(Of Integer, ULong)(obj.VisualGroupID, obj.phash))

                    counter += 1
                    counterfiles += obj.hashvalues.Count

                Catch ex As Exception

                End Try

                dbIterator.Next()

            End While


        Catch ex As Exception
            Me.Invoke(Sub() Me.TextBox1.AppendText("Error Loading Database " & ex.Message))

        Finally

            SyncObjLockImageSignaturesDatabase.ReleaseWriterLock()
            SyncObjLockPhashsDatabase.ReleaseWriterLock()
        End Try

        watcher.Stop()

        If isdebug Then
            Me.Invoke(Sub() Me.TextBox1.AppendText("Loaded Visual Copies signatures: " & counter & vbCrLf))
            Me.Invoke(Sub() Me.TextBox1.AppendText("Loaded Image signatures: " & counterfiles & vbCrLf))
            Me.Invoke(Sub() Me.TextBox1.AppendText("Elapsed time: " & watcher.ElapsedMilliseconds & vbCrLf))
            Me.Invoke(Sub() Me.TextBox1.AppendText("Signatures/millisec " & counter / watcher.ElapsedMilliseconds & vbCrLf))
        End If




    End Sub

    Public ReadOnly Property HashvalueHex(sha1hash As Byte()) As String
        Get

            Try
                Return BitConverter.ToString(sha1hash).Replace("-", "")
            Catch ex As Exception

            End Try

        End Get

    End Property



#Region "TEST"


    'Test Functions

    Private Sub Button1_Click(sender As Object, e As EventArgs) Handles Button1.Click

        Dim o As New OpenFileDialog
        o.ShowDialog()
        Dim JCTHash() As Double     'Hash for similar images

        Using bmp As New Bitmap(o.FileName)

            Dim cedd As New CEDD
            Dim FCTH As New FCTH_Descriptor.FCTH
            If bmp.PixelFormat <> PixelFormat.Format24bppRgb Then

                Using bmp2 As Bitmap = bmp.Clone(New Rectangle(0, 0, bmp.Width, bmp.Height), PixelFormat.Format24bppRgb)
                    Dim Ceddres As Double() = cedd.Apply(bmp2)
                    Dim FCTHres As Double() = FCTH.Apply(bmp2, 2)
                    JCTHash = FCTH.JointHistograms(Ceddres, FCTHres)

                End Using

            Else
                Dim Ceddres As Double() = cedd.Apply(bmp)
                Dim FCTHres As Double() = FCTH.Apply(bmp, 2)
                JCTHash = FCTH.JointHistograms(Ceddres, FCTHres)
            End If



        End Using


    End Sub

    Private Sub Button2_Click(sender As Object, e As EventArgs) Handles Button2.Click
        Dim o As New OpenFileDialog
        o.ShowDialog()
        Dim JCTHash() As Double     'Hash for similar images

        Using bmp As New Bitmap(o.FileName)

            Dim cedd As New CEDD
            Dim FCTH As New FCTH_Descriptor.FCTH
            If bmp.PixelFormat <> PixelFormat.Format24bppRgb Then

                Using bmp2 As Bitmap = bmp.Clone(New Rectangle(0, 0, bmp.Width, bmp.Height), PixelFormat.Format24bppRgb)
                    Dim Ceddres As Double() = cedd.Apply(bmp2)
                    Dim FCTHres As Double() = FCTH.Apply(bmp2, 2)
                    JCTHash = FCTH.JointHistograms(Ceddres, FCTHres)

                End Using

            Else
                Dim Ceddres As Double() = cedd.Apply(bmp)
                Dim FCTHres As Double() = FCTH.Apply(bmp, 2)
                JCTHash = FCTH.JointHistograms(Ceddres, FCTHres)
            End If

            Dim jcrthashbyte(JCTHash.Length - 1) As UShort
            For i As Integer = 0 To JCTHash.Length - 1
                jcrthashbyte(i) = UShort.MaxValue * JCTHash(i)

            Next
            VisuallySimilarImageSignaturesDatabase.Query(jcrthashbyte)

        End Using

    End Sub

    Public Function CompressImage(image As Bitmap) As MemoryStream

        Dim str As New MemoryStream
        Try
            image.Save(str, System.Drawing.Imaging.ImageFormat.Jpeg)

        Catch ex As Exception

        End Try


        Return str


    End Function

    Private Sub Button3_Click(sender As Object, e As EventArgs) Handles Button3.Click
        Dim o As New OpenFileDialog
        o.ShowDialog()
        Dim JCTHash() As Double     'Hash for similar images

        Dim obj As New ImageRequest

        Using str As New FileStream(o.FileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1 * 1024 * 1024)

            'Hash the file
            Try
                Using _md5 As MD5 = MD5.Create
                    obj.Hashvalue = _md5.ComputeHash(str)
                End Using

            Catch ex As Exception

            End Try
        End Using

        Using bmp As New Bitmap(o.FileName)

            obj.JCTHash = VisuallySimilarImageSignaturesDatabase.DoubleToUShortArray(VisuallySimilarImageSignaturesDatabase.CalculateVector(bmp))
            obj.frameindex = -1
            obj.Image = CompressImage(bmp).ToArray
            obj.phash = ImagePhash.ComputeDctHash(bmp.ToLuminanceImage)

            AddImageQueu.Enqueue(MessagePackSerializer.Serialize(Of ImageRequest)(obj)) '
            Handler.Set()

        End Using



    End Sub

#End Region

End Class
