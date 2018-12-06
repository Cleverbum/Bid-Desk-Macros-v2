﻿Imports System.Diagnostics
Imports Microsoft.Office.Interop.Outlook
Imports MySql.Data.MySqlClient

Public Class ThisAddIn
    Public Const server As String = "mklefass-sql2.database.windows.net"
    Public Const user As String = "mklefass"
    Public Const password As String = "nuNDCb4MqmU66j58"
    Public Const database As String = "TutorialDB"
    Public Const defaultTable As String = "Customers"
    Public Const port As Integer = 1433
    Public Const searchType As StringComparison = vbTextCompare



    Private Sub ThisAddIn_Startup() Handles Me.Startup

    End Sub

    Private Sub ThisAddIn_Shutdown() Handles Me.Shutdown

    End Sub
    Sub MoveBasedOnDealID(Optional suppressWarnings As Boolean = False)

        Dim obj As Object, success As Boolean
        Dim msg As Outlook.MailItem

        '  Dim olApp As New Outlook.Application 'new throws security error
        Dim DealID As String, targetFolder As String
        Dim olCurrExplorer As Outlook.Explorer
        Dim olCurrSelection As Outlook.Selection


        '  Set olNameSpace = olApp.GetNamespace("MAPI")
        olCurrExplorer = Application.ActiveExplorer
        olCurrSelection = olCurrExplorer.Selection

        For m = 1 To olCurrSelection.Count
            obj = olCurrSelection.Item(m)
            If TypeName(obj) = "MailItem" Then
                msg = obj
                DealID = FindDealID(msg.Subject, msg.Body)
                If DealID = "" Then Exit Sub
                targetFolder = GetFolderbyDeal(DealID, suppressWarnings)

                success = MoveToFolder(targetFolder, msg)
            End If
        Next m
    End Sub


End Class
