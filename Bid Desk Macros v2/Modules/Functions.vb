﻿Imports System.Diagnostics
Imports Microsoft.Office.Interop.Outlook

Partial Class ThisAddIn
    Public sqlInterface As New ClsDatabase(ThisAddIn.server, ThisAddIn.user,
                                   ThisAddIn.database, ThisAddIn.port)


    Public Function MoveToFolder(folderName As String, thisMailItem As MailItem, Optional suppressWarnings As Boolean = False) As Boolean
        Dim mailboxNameString As String
        mailboxNameString = "Martin.Klefas@insight.com"

        Dim olApp As New Outlook.Application
        Dim olNameSpace As Outlook.NameSpace

        Dim olDestFolder As Outlook.MAPIFolder


        olNameSpace = olApp.GetNamespace("MAPI")

        'OLD: Set olDestFolder = olNameSpace.Folders(mailboxNameString).Folders("Martins Emails").Folders(folderName)
        Try
            olDestFolder = olNameSpace.GetDefaultFolder(Outlook.OlDefaultFolders.olFolderInbox).Folders("Bids").Folders(folderName)
            thisMailItem.Move(olDestFolder)
            MoveToFolder = True
        Catch
            Try
                olDestFolder = olNameSpace.Folders(mailboxNameString).Folders("Inbox").Folders("Bids").Folders.Add(folderName)
                thisMailItem.Move(olDestFolder)
                MoveToFolder = True
            Catch
                If Not suppressWarnings Then MsgBox("Couldn't move the mail for some reason...")
                MoveToFolder = False
            End Try
        End Try
    End Function



    Public Function FindDealID(MsgSubject As String, msgBody As String, Optional completeAutonomy As Boolean = False) As String
        Dim myAr As String(), i As Integer, myArTwo As String()
        myAr = Split(MsgSubject, " ")
        Dim DealIDForm As New DealIdent


        For i = LBound(myAr) To UBound(myAr)
            '~~> This will give you the contents of your email
            '~~> on separate lines
            myAr(i) = Trim(myAr(i))

            If Len(myAr(i)) > 4 Then
                If myAr(i).StartsWith("P00", ThisAddIn.searchType) Or
                    myAr(i).StartsWith("E00", ThisAddIn.searchType) Or
                    myAr(i).StartsWith("NQ", ThisAddIn.searchType) Then
                    If Mid(LCase(myAr(i)), Len(myAr(i)) - 2, 2) = "-v" Then myAr(i) = Left(myAr(i), Len(myAr(i)) - 3)
                    DealIDForm.DealID.Text = Trim(myAr(i))
                End If
                If myAr(i).StartsWith("REGI-", ThisAddIn.searchType) Or
                    myAr(i).StartsWith("REGE-", ThisAddIn.searchType) Then
                    DealIDForm.DealID.Text = Trim(myAr(i))
                End If
            End If
        Next i

        If DealIDForm.DealID.Text = "" Then
            myAr = Split(msgBody, vbCrLf)
            For i = LBound(myAr) To UBound(myAr)
                '~~> This will give you the contents of your email
                '~~> on separate lines
                If Len(myAr(i)) > 8 Then
                    If myAr(i).StartsWith("Deal ID:", ThisAddIn.searchType) Then
                        myArTwo = Split(myAr(i))
                        DealIDForm.DealID.Text = Trim(myArTwo(2))
                    End If
                End If
            Next
        End If

        If DealIDForm.DealID.Text = "" AndAlso i + 3 < UBound(myAr) Then
            If myAr(i) = "Quote" And myAr(i + 1) = "Review" And myAr(i + 2) = "Quote" Then
                DealIDForm.DealID.Text = Trim(myAr(i + 4))
            End If
            If myAr(i) = "QUOTE" And myAr(i + 1) = "Deal" And myAr(i + 3) = "Version" Then
                DealIDForm.DealID.Text = Trim(myAr(i + 2))
            End If

        End If

        If Not completeAutonomy Then
            DealIDForm.ShowDialog()

        End If



        FindDealID = DealIDForm.DealID.Text
    End Function

    Private Function RecordWaitTime(receivedTime As Date, completedTime As Date, person As String) As String


        Dim tmpDict As New Dictionary(Of String, String) From {
            {"WaitingFor", person},
            {"Received", receivedTime},
            {"Completed", completedTime}
        }


        If sqlInterface.Add_Data(tmpDict, "wait_times") Then
            Return PrettyString(completedTime - receivedTime)
        Else
            Return "Adding the wait time failed"
        End If


    End Function


    Function CreateDealRecord(Mail As Outlook.MailItem) As Dictionary(Of String, String)
        Dim NewDealForm As New newDeal
        Dim requestorName As String, Vendor As String, ccNames As String
        Dim ReplyMail As MailItem = Mail.ReplyAll
        Dim tCreateDealRecord As Dictionary(Of String, String)

        If NewDealForm.ShowDialog() = Windows.Forms.DialogResult.OK Then ' Show and wait
            Dim toNames As String(), rName() As String

            toNames = Split(ReplyMail.To, ";") ' Split out each recipient

            If InStr(toNames(0), ",") > 1 Then ' Some email names are "fName, lName" others aren't

                rName = Split(toNames(0), ",")
                requestorName = Trim(rName(1)) & " " & Trim(rName(0))
            Else
                requestorName = Trim(toNames(0))
            End If

            If NewDealForm.DellOption.Checked Then
                Vendor = "Dell"
            ElseIf NewDealForm.HPIOption.Checked Then
                Vendor = "HPI"
            Else
                Vendor = "HPE"
            End If

            ccNames = ReplyMail.CC
            For i = 1 To UBound(toNames) ' append the second and later "to" names to the CC list
                ccNames = ccNames & "; " & toNames(i)
            Next

            Dim bIngram As Byte = BooltoByte(NewDealForm.cIngram.Checked)
            Dim bTechData As Byte = BooltoByte(NewDealForm.cTechData.Checked)
            Dim bWestcoast As Byte = BooltoByte(NewDealForm.cWestcoast.Checked)

            tCreateDealRecord = New Dictionary(Of String, String) From {
                {"AMEmailAddress", Mail.SenderEmailAddress},
                {"AM", requestorName},
                {"Customer", NewDealForm.CustomerName.Text},
                {"Vendor", Vendor},
                {"DealID", NewDealForm.DealID.Text},
                {"Ingram", bIngram},
                {"Techdata", bTechData},
                {"Westcoast", bWestcoast},
                {"CC", ccNames},
                {"Status", "Submitted to Vendor"},
                {"StatusDate", DateTime.Now().ToString},
                {"Date", DateTime.Now().ToString}
            }

            Dim ndt As New clsNextDeskTicket.ClsNextDeskTicket

            tCreateDealRecord.Add("NDT", ndt.CreateTicket(1, MakeTicketData(tCreateDealRecord, ReplyMail)).ToString)
            Dim aliases As String = ""
            'add people to notify
            For Each recipient As Outlook.Recipient In ReplyMail.Recipients
                Try
                    aliases &= recipient.AddressEntry.GetExchangeUser.Alias & ";"
                Catch
                    ShoutError("Could not find alias for: " & recipient.ToString)
                End Try
            Next
            ndt.AddToNotify(aliases)

            'update ticket with bid number & original email
            ndt.AttachMail(Mail, "Deal ID  " & tCreateDealRecord("DealID") & "was submitted to " & tCreateDealRecord("Vendor") & " based on the information in the attached email")

            If sqlInterface.Add_Data(tCreateDealRecord) Then
                tCreateDealRecord.Add("Result", "Success")
            Else
                tCreateDealRecord.Add("Result", "Failed")
            End If

            Return tCreateDealRecord

        Else
            Return New Dictionary(Of String, String) From {
                {"Result", "Cancelled"}
            }



        End If






    End Function



    Private Function MakeTicketData(DealDict As Dictionary(Of String, String), email As Outlook.MailItem) As Dictionary(Of String, String)

        Dim requestor As Outlook.ExchangeUser
        requestor = email.Recipients(0).AddressEntry.GetExchangeUser



        MakeTicketData = New Dictionary(Of String, String) From {
            {"Short Description", DealDict("Vendor") & "Bid for " & DealDict("Customer")},
            {"Vendor", DealDict("Vendor")},
            {"Client Name", DealDict("Customer")},
            {"Sales Name", requestor.Name},
            {"Sales Number", requestor.BusinessTelephoneNumber},
            {"Sales Email", requestor.PrimarySmtpAddress},
            {"Description", "This is a copy of a request sent in by email, the original email will be attached. The request has been completed, and the results of these actions will be automatically added when ready."}
        }

    End Function

    Private Function MakeTicketData(DealID As String) As Dictionary(Of String, String)

        Dim tmp = sqlInterface.SelectData_Dict("*", "DealID = '" & DealID & "'")

        Dim DealDict As Dictionary(Of String, String) = tmp(0)

        Dim requestor As Outlook.ExchangeUser

        requestor = Globals.ThisAddIn.Application.Session.CreateRecipient(DealDict("AM")).AddressEntry

        MakeTicketData = New Dictionary(Of String, String) From {
            {"Short Description", DealDict("Vendor") & "Bid for " & DealDict("Customer")},
            {"Vendor", DealDict("Vendor")},
            {"Client Name", DealDict("Customer")},
            {"Sales Name", requestor.Name},
            {"Sales Number", requestor.BusinessTelephoneNumber},
            {"Sales Email", requestor.PrimarySmtpAddress},
            {"Description", "We have received an expiry notice from the vendor as attached."}
        }


    End Function

    Sub ShoutError(errorText As String, Optional SuppressWarnings As Boolean = True)
        Debug.WriteLine(errorText)

        If Not SuppressWarnings Then MsgBox(errorText)


    End Sub

    Function GetSelection() As Outlook.Selection
        Dim olCurrExplorer As Outlook.Explorer


        olCurrExplorer = Application.ActiveExplorer
        GetSelection = olCurrExplorer.Selection

    End Function


    Function IsDealDead(DealID As String) As Boolean

        Dim tmp As String
        tmp = sqlInterface.SelectData("Status", "DealID = '" & DealID & "'")

        Return tmp.ToLower.Contains("deal lost")


    End Function

    Function GetCurrentItem() As Object
        Select Case True
            Case IsExplorer(Application.ActiveWindow)
                GetCurrentItem = Application.ActiveExplorer.Selection.Item(1)
            Case IsInspector(Application.ActiveWindow)
                GetCurrentItem = Application.ActiveInspector.CurrentItem
            Case Else
                GetCurrentItem = Nothing
        End Select
    End Function
    Function IsExplorer(itm As Object) As Boolean
        IsExplorer = (TypeName(itm) = "Explorer")
    End Function
    Function IsInspector(itm As Object) As Boolean
        IsInspector = (TypeName(itm) = "Inspector")
    End Function

    Function WriteGreeting(myTime As Date, Optional toName As String = "") As String
        Dim currenthour As Integer

        currenthour = Microsoft.VisualBasic.DateAndTime.Hour(myTime)

        WriteGreeting = "Good "

        If currenthour < 12 Then
            WriteGreeting &= "Morning"
        ElseIf currenthour >= 17 Then
            WriteGreeting &= "Evening"
        Else
            WriteGreeting &= "Afternoon"
        End If

        If toName = "" Or toName.ToLower = "insight" Then
            WriteGreeting &= ","
        Else
            WriteGreeting &= " " & toName & ","
        End If


    End Function

    Function MyResolveName(lookupName As String) As Outlook.AddressEntry
        Dim oNS As Outlook.NameSpace, newLookupName As String

        newLookupName = lookupName

        Dim noChangeNamesStr As String
        noChangeNamesStr = "LGN.Enquiries;Lorrae.Tomlinson@insight.com;NHSSupport;TeamWhite.UK;Insight ACPO TAM Team;Insight Met Police Team;Mel Wardle;Insight Capgemini Team;Insight Police Team;ipt@insight.com;_iuk-72-2-brianboys@insight.com"

        Dim nameArry = Split(lookupName, " ")
        If UBound(nameArry) > 0 Then
            newLookupName = nameArry(1) & ", " & nameArry(0)
        End If
        oNS = Application.GetNamespace("MAPI")

        If lookupName = "Not Defined" Or lookupName = "TP2 Enquiries" Then newLookupName = "Klefas, Martin"
        If lookupName = "Andy Walsh" Then newLookupName = "Walsh, Andrew"
        If lookupName = "NHS Solutions" Then newLookupName = "NHSSolutions@Insight.com"

        If noChangeNamesStr.ToLower.Contains(lookupName.ToLower) Then newLookupName = lookupName

        Return Globals.ThisAddIn.Application.Session.CreateRecipient(newLookupName).AddressEntry

    End Function

    Function BooltoByte(ByVal tBoolean As Boolean) As Byte
        If tBoolean Then
            BooltoByte = 1
        Else
            BooltoByte = 0
        End If
    End Function

    Function WriteSubmitMessage(ByVal DealDetails As Dictionary(Of String, String)) As String
        WriteSubmitMessage = Replace(SubmitMessage, "%DEALID%", DealDetails("DealID"))
        WriteSubmitMessage = Replace(WriteSubmitMessage, "%VENDOR%", DealDetails("Vendor"))

        If DealDetails.ContainsKey("NDT") Then
            WriteSubmitMessage = Replace(WriteSubmitMessage, "%NDT%", Replace(NDTCreateMessage, "%NDT%", DealDetails("NDT")))
        Else
            WriteSubmitMessage = Replace(WriteSubmitMessage, "%NDT%", noNDTMessage)
        End If

        WriteSubmitMessage = WriteSubmitMessage & drloglink
    End Function
End Class



