using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace WFApi
{
    public enum WFRights 
    {
        INIT    = 0,    // Start Workflow by submitting 1st document set version
        APPROVE = 1,    // Approve the current document set version
        REVIEW  = 2,    // Submit an updated document set
        SIGNOFF = 3,    // Conclude workflow successfully
        ABORT   = 4     // Abort workflow
    };

    [FunctionOutput]
    public class GetRightOutput : IFunctionOutputDTO
    {
        [Parameter("address", "user", 1)]
        public string user { get; set; }

        [Parameter("uint8", "right", 2)]
        public uint right { get; set; }
    }

    public class WFBuilder
    {
        protected WFWallet m_myWallet;
        protected Nethereum.Contracts.Contract m_contract;

        public WFBuilder(WFWallet wallet)
        {
            m_myWallet = wallet;
            m_contract = wallet.W3.Eth.GetContract(WF.BUILDER_ABI, WF.BUILDER_ADDR);
        }

        public async Task<UInt32> GetEndState(UInt32 initial, UInt32 edge)
        {
            var func = m_contract.GetFunction("states");
            return await func.CallAsync<UInt32>(initial, edge);
        }

        public async Task<UInt32> GetTotalStates()
        {
            var func = m_contract.GetFunction("getTotalStates");
            return await func.CallAsync<UInt32>();
        }

        public async Task<UInt32> GetTotalEdges(UInt32 stateid)
        {
            var func = m_contract.GetFunction("getTotalEdges");
            return await func.CallAsync<UInt32>(stateid);
        }

        public async Task<UInt32> GetTotalRights(UInt32 stateid, UInt32 edgeid)
        {
            var func = m_contract.GetFunction("getTotalRights");
            return await func.CallAsync<UInt32>(stateid, edgeid);
        }

        public async Task<GetRightOutput> GetRight(UInt32 stateid, UInt32 edgeid, UInt32 rightid)
        {
            var func = m_contract.GetFunction("getRight");
            return await func.CallDeserializingToObjectAsync<GetRightOutput>(stateid, edgeid, rightid);
        }

        public async Task<bool> HasRight(UInt32 state1, UInt32 state2, string user, WFRights right)
        {
            var func = m_contract.GetFunction("hasRight");
            return await func.CallAsync<bool>(state1, state2, user, right);
        }

        public async Task<uint> GetUSN()
        {
            var func = m_contract.GetFunction("usn");
            return await func.CallAsync<uint>();
        }

        public async Task<BigInteger> EstimateAddRight(UInt32 stateid, UInt32 edgeid, string user, WFRights right, EstTyp typ = EstTyp.GAS, BigInteger? gasPrice = null)
        {
            Nethereum.Contracts.Function func = m_contract.GetFunction("addRight");
            var gas = await func.EstimateGasAsync(m_myWallet.Address, null, null, stateid, edgeid, user, right);

            return WF.Estimate(gas, typ, gasPrice);
        }

        public async Task<BigInteger> EstimateRemoveRight(UInt32 stateid, UInt32 edgeid, string user, WFRights right, EstTyp typ = EstTyp.GAS, BigInteger? gasPrice = null)
        {
            Nethereum.Contracts.Function func = m_contract.GetFunction("removeRight");
            var gas = await func.EstimateGasAsync(m_myWallet.Address, null, null, stateid, edgeid, user, right);

            return WF.Estimate(gas, typ, gasPrice);
        }

        public async Task<string> AddRight(UInt32 stateid, UInt32 edgeid, string user, WFRights right, BigInteger? gasPrice = null)
        {
            Nethereum.Contracts.Function func = m_contract.GetFunction("addRight");
            var gas = await func.EstimateGasAsync(m_myWallet.Address, null, null, stateid, edgeid, user, right);
            var weiGP = WF.FinalGasPrice(gasPrice);
            var recpt = await func.SendTransactionAndWaitForReceiptAsync(
                                        m_myWallet.Address, gas, weiGP, null, null,
                                        stateid, edgeid, user, right);

            if ((recpt == null) || (recpt.Status.Value == 0))
                return null;

            RecordTxFee(recpt.TransactionHash, recpt.GasUsed.Value * weiGP.Value);
            return recpt.TransactionHash;
        }

        public async Task<string> RemoveRight(UInt32 stateid, UInt32 edgeid, string user, WFRights right, BigInteger? gasPrice = null)
        {
            Nethereum.Contracts.Function func = m_contract.GetFunction("removeRight");
            var gas = await func.EstimateGasAsync(m_myWallet.Address, null, null, stateid, edgeid, user, right);
            var weiGP = WF.FinalGasPrice(gasPrice);
            var recpt = await func.SendTransactionAndWaitForReceiptAsync(
                                        m_myWallet.Address, gas, weiGP, null, null,
                                        stateid, edgeid, user, right);

            if ((recpt == null) || (recpt.Status.Value == 0))
                return null;

            RecordTxFee(recpt.TransactionHash, recpt.GasUsed.Value * weiGP.Value);
            return recpt.TransactionHash;
        }

        public async Task<List<string>> GetParticipants()
        {
            List<string> addrList = new List<string>();

            UInt32 states = await GetTotalStates();
            for (UInt32 cntStates = 0; cntStates < states; ++cntStates)
            {
                UInt32 edges = await GetTotalEdges(cntStates);
                for (UInt32 cntEdges = 0; cntEdges < edges; ++cntEdges)
                {
                    UInt32 rights = await GetTotalRights(cntStates, cntEdges);
                    for (UInt32 cntRights = 0; cntRights < rights; ++cntRights)
                    {
                        GetRightOutput rightInfo = await GetRight(cntStates, cntEdges, cntRights);

                        if (addrList.Find(x => x == rightInfo.user) == default(string))
                            addrList.Add(rightInfo.user);
                    }
                }
            }

            return addrList;
        }

        protected void RecordTxFee(string hash, BigInteger fee)
        {
            TxFeeHistory.Add(hash, fee);
        }
    }
}
