﻿Imports System.Windows.Forms
Imports Microsoft.Office.Interop.Outlook
Imports String_Extensions

Public Class AddDeal
    Private mail As MailItem

    Public Sub New(mail As MailItem)
        InitializeComponent()
        Me.mail = mail
    End Sub

    Private Sub CommandButton1_Click() Handles OKButton.Click
        CustomerName.Text = TrimExtended(CustomerName.Text)
        DealID.Text = TrimExtended(DealID.Text)

        DisableButtons()

        BackgroundWorker1.RunWorkerAsync()
    End Sub

    Private Sub Button2_Click() Handles tCancelButton.Click

        Me.DialogResult = DialogResult.Cancel
        Me.Hide()
    End Sub

    Private Sub UserForm_Activate() Handles Me.Activated


        Dim strClip As String


        strClip = My.Computer.Clipboard.GetText

        Me.DealID.Text = FindDealID(strClip)
        Me.CustomerName.Text = FindCustomer(strClip)
        Select Case FindVendor(strClip)
            Case "HPI"
                Call CheckOnly(HPIOption)
            Case "HPE"
                Call CheckOnly(HPEOption)
            Case "Dell"
                Call CheckOnly(DellOption)


        End Select

    End Sub
    Private Sub CheckOnly(toCheck As RadioButton)
        For Each tControl As Control In VendorGroupBox.Controls
            If TypeName(tControl) = "RadioButton" Then
                Dim rButton As RadioButton = tControl
                rButton.Checked = False
            End If
        Next
        toCheck.Checked = True
    End Sub
    Private Sub UserForm_QueryClose(Cancel As Integer, CloseMode As Integer)

        Me.Hide()
    End Sub


    Private Sub TextBox1_KeyDown(sender As Object, e As Windows.Forms.KeyEventArgs) Handles CustomerName.KeyDown
        If e.KeyCode = Keys.Enter Then
            CommandButton1_Click()
        End If
    End Sub
    Private Sub TextBox2_KeyDown(sender As Object, e As Windows.Forms.KeyEventArgs) Handles CustomerName.KeyDown
        If e.KeyCode = Keys.Enter Then
            CommandButton1_Click()
        End If
    End Sub

    Private Sub Button2_Click(sender As Object, e As EventArgs) Handles tCancelButton.Click
        Me.Hide()
    End Sub

    Private Sub BackgroundWorker1_DoWork(sender As Object, e As System.ComponentModel.DoWorkEventArgs) Handles BackgroundWorker1.DoWork
        Dim requestorName As String, Vendor As String, ccNames As String
        Dim ReplyMail As MailItem = Mail.ReplyAll
        Dim tCreateDealRecord As Dictionary(Of String, String)


        Dim toNames As String(), rName() As String

        UpdateTitle("Preparing Details...")

        toNames = Split(ReplyMail.To, ";") ' Split out each recipient

        If InStr(toNames(0), ",") > 1 Then ' Some email names are "fName, lName" others aren't

            rName = Split(toNames(0), ",")
            requestorName = TrimExtended(rName(1)) & " " & TrimExtended(rName(0))
        Else
            requestorName = TrimExtended(toNames(0))
        End If

        If Me.DellOption.Checked Then
            Vendor = "Dell"
        ElseIf Me.HPIOption.Checked Then
            Vendor = "HPI"
        Else
            Vendor = "HPE"
        End If

        ccNames = ReplyMail.CC
        For i = 1 To UBound(toNames) ' append the second and later "to" names to the CC list
            ccNames = ccNames & "; " & toNames(i)
        Next

        Dim bIngram As Byte = Globals.ThisAddIn.BooltoByte(Me.cIngram.Checked)
        Dim bTechData As Byte = Globals.ThisAddIn.BooltoByte(Me.cTechData.Checked)
        Dim bWestcoast As Byte = Globals.ThisAddIn.BooltoByte(Me.cWestcoast.Checked)

        tCreateDealRecord = New Dictionary(Of String, String) From {
                {"AMEmailAddress", mail.SenderEmailAddress},
                {"AM", requestorName},
                {"Customer", Me.CustomerName.Text},
                {"Vendor", Vendor},
                {"DealID", Me.DealID.Text},
                {"Ingram", bIngram},
                {"Techdata", bTechData},
                {"Westcoast", bWestcoast},
                {"CC", ccNames},
                {"Status", "Submitted to Vendor"},
                {"StatusDate", DateTime.Now().ToString("yyyyMMdd HH:mm:ss")},
                {"Date", DateTime.Now().ToString("yyyyMMdd HH:mm:ss")}
            }

        Dim ndt As New clsNextDeskTicket.ClsNextDeskTicket(False, True, ThisAddIn.timingFile)

        UpdateTitle("Creating Ticket...")
        tCreateDealRecord.Add("NDT", ndt.CreateTicket(1, Globals.ThisAddIn.MakeTicketData(tCreateDealRecord, ReplyMail)).ToString)
        ndt.Move("Public Sector")

        Dim aliases As String = ""
        'add people to notify
        For Each recipient As Outlook.Recipient In ReplyMail.Recipients
            Try
                aliases &= recipient.AddressEntry.GetExchangeUser.Alias & ";"
            Catch
                Globals.ThisAddIn.ShoutError("Could not find alias for: " & recipient.ToString)
            End Try
        Next

        UpdateTitle("Adding Notify...")

        ndt.AddToNotify(aliases)

        UpdateTitle("Attaching Info...")

        'update ticket with bid number & original email
        ndt.AttachMail(Mail, "Deal ID  " & tCreateDealRecord("DealID") & "was submitted to " & tCreateDealRecord("Vendor") & " based on the information in the attached email")

        tCreateDealRecord.Remove("AMEmailAddress")

        If Globals.ThisAddIn.sqlInterface.Add_Data(tCreateDealRecord) > 0 Then
            Dim rFName As String() = Split(tCreateDealRecord("AM"))
            Dim mygreeting As String
            mygreeting = Globals.ThisAddIn.WriteGreeting(Now(), CStr(rFName(0)))



            With ReplyMail
                .HTMLBody = mygreeting & Globals.ThisAddIn.WriteSubmitMessage(tCreateDealRecord) & .HTMLBody
                .Subject = .Subject & " - " & tCreateDealRecord("DealID")
                .Display() ' or .Send
            End With
            Globals.ThisAddIn.MoveToFolder(TrimExtended(tCreateDealRecord("AM")), mail)
        Else
            tCreateDealRecord.Add("Result", "Failed")
        End If


        UpdateTitle("All Done!")

        CloseMe()

    End Sub

    Sub DisableButtons()

        OKButton.Enabled = False
        tCancelButton.Enabled = False
        CustomerName.Enabled = False
        HPIOption.Enabled = False
        HPEOption.Enabled = False
        DellOption.Enabled = False
        cIngram.Enabled = False
        cTechData.Enabled = False
        cWestcoast.Enabled = False
        DealID.Enabled = False


    End Sub

    Private Sub CloseMe()

        ' InvokeRequired required compares the thread ID of the'
        ' calling thread to the thread ID of the creating thread.'
        ' If these threads are different, it returns true.'
        If Me.Label1.InvokeRequired Then
            Dim d As New CloseMeCallback(AddressOf CloseMe)
            Me.Invoke(d, New Object() {})
        Else

            Me.Close()

        End If
    End Sub
    Delegate Sub CloseMeCallback()
    Private Sub UpdateTitle(ByVal [NewTitle] As String)

        ' InvokeRequired required compares the thread ID of the'
        ' calling thread to the thread ID of the creating thread.'
        ' If these threads are different, it returns true.'
        If Me.Label1.InvokeRequired Then
            Dim d As New UpdateTitleCallback(AddressOf UpdateTitle)
            Me.Invoke(d, New Object() {[NewTitle]})
        Else

            Me.Text = NewTitle

        End If
    End Sub
    Delegate Sub UpdateTitleCallback(ByVal [NewTitle] As String)
End Class