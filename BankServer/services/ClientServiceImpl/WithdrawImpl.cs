﻿using BankServer.utils;
using Grpc.Core;

namespace BankServer.services
{
    public partial class ClientServiceImpl : ClientService.ClientServiceBase
    {
        public override Task<WithdrawResp> Withdraw(WithdrawReq request, ServerCallContext context)
        {
            Logger.LogDebug("Withdraw received.");
            //Logger.LogDebug(_state.IsFrozen().ToString());
            if (!_state.IsFrozen())
            {
                WithdrawResp response = doWithdraw(request);
                Logger.LogDebug("End of Withdraw");
                return Task.FromResult(response);
            }
            // Request got queued and will be handled later
            throw new Exception("The server is frozen.");
        }

        public WithdrawResp doWithdraw(WithdrawReq request)
        {
            string response = _bankManager.Withdraw((int)request.Client.ClientID, request.Amount);
            return new WithdrawResp() { Response = response };
        }
    }
}