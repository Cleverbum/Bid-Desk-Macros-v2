﻿Imports System.Diagnostics
Imports Microsoft.Office.Tools.Ribbon

Public Class Ribbon1

    Private Sub Ribbon1_Load(ByVal sender As System.Object, ByVal e As RibbonUIEventArgs) Handles MyBase.Load

    End Sub

    Private Sub Button1_Click(sender As Object, e As RibbonControlEventArgs) Handles Button1.Click
        Dim sqlInterface As New ClsDatabase(ThisAddIn.server, ThisAddIn.user,
                                   ThisAddIn.database, ThisAddIn.password)
        Dim tmp As String
        tmp = sqlInterface.SelectData("Name", "Location = Australia")
        Debug.WriteLine(tmp)
    End Sub

    Private Sub Button2_Click(sender As Object, e As RibbonControlEventArgs) Handles Button2.Click
        Globals.ThisAddIn.MoveBasedOnDealID()
    End Sub
End Class
