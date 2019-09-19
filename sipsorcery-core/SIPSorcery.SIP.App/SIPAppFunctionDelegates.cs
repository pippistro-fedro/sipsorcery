﻿// ============================================================================
// FileName: SIPFunctionDelegates.cs
//
// Description:
// A list of function delegates that are used by the SIP Server Agents.
//
// Author(s):
// Aaron Clauson
//
// History:
// 14 Nov 2008	Aaron Clauson	Created.
//
// License: 
// This software is licensed under the BSD License http://www.opensource.org/licenses/bsd-license.php
//
// Copyright (c) 2008 Aaron Clauson (aaron@sipsorcery.com), SIP Sorcery PTY LTD, Hobart, Australia (www.sipsorcery.com)
// All rights reserved.
//
// Redistribution and use in source and binary forms, with or without modification, are permitted provided that 
// the following conditions are met:
//
// Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer. 
// Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following 
// disclaimer in the documentation and/or other materials provided with the distribution. Neither the name of SIP Sorcery PTY LTD. 
// nor the names of its contributors may be used to endorse or promote products derived from this software without specific 
// prior written permission. 
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, 
// BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. 
// IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, 
// OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, 
// OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, 
// OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE 
// POSSIBILITY OF SUCH DAMAGE.
// ============================================================================

using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Net;

namespace SIPSorcery.SIP.App
{
    public delegate void SIPMonitorLogDelegate(SIPMonitorEvent monitorEvent);
    public delegate void SIPMonitorMachineLogDelegate(SIPMonitorMachineEvent machineEvent);
    public delegate bool SIPMonitorAuthenticationDelegate(string username, string password);    // Delegate to authenticate connections to the SIP Monitor Server.
    public delegate void DialogueBridgeCreatedDelegate(SIPDialogue clientDialogue, SIPDialogue forwardedDialogue, string owner);
    public delegate void DialogueBridgeClosedDelegate(string dialogueId, string owner);
    public delegate void IPAddressChangedDelegate(IPAddress newIPAddress);
    public delegate void QueueNewCallDelegate(ISIPServerUserAgent uas);
    public delegate void BlindTransferDelegate(SIPDialogue deadDialogue, SIPDialogue orphanedDialogue, SIPDialogue answeredDialogue);

    // SIP User Agent Delegates.
    public delegate void SIPCallResponseDelegate(ISIPClientUserAgent uac, SIPResponse sipResponse);
    public delegate void SIPCallFailedDelegate(ISIPClientUserAgent uac, string errorMessage);
    public delegate void SIPUASStateChangedDelegate(ISIPServerUserAgent uas, SIPResponseStatusCodesEnum statusCode, string reasonPhrase);

    // Get SIP account(s) from external sources delegate.
    public delegate SIPAccount GetSIPAccountDelegate(string username, string domain);
    public delegate List<SIPAccount> GetSIPAccountsForUserDelegate(string username, string domain, int offset, int limit);
    public delegate List<SIPAccount> GetSIPAccountsForOwnerDelegate(string owner, int offset, int limit);

    // Authorisation delegates.
    public delegate SIPRequestAuthenticationResult SIPAuthenticateRequestDelegate(SIPEndPoint localSIPEndPoint, SIPEndPoint remoteEndPoint, SIPRequest sipRequest, SIPAccount sipAccount, SIPMonitorLogDelegate log);

    // SIP Presence delegates.
    public delegate int SIPRegistrarBindingsCountDelegate(Guid sipAccountID);
    public delegate object SIPAssetGetPropertyByIdDelegate<T>(Guid id, string propertyName);
}
