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

        public async Task<uint> GetEndState(uint initial, uint edge)
        {
            var func = m_contract.GetFunction("states");
            return await func.CallAsync<uint>(initial, edge);
        }

        public async Task<uint> GetTotalStates()
        {
            var func = m_contract.GetFunction("getTotalStates");
            return await func.CallAsync<uint>();
        }

        public async Task<uint> GetTotalEdges(uint stateid)
        {
            var func = m_contract.GetFunction("getTotalEdges");
            return await func.CallAsync<uint>(stateid);
        }

        public async Task<uint> GetTotalRights(uint stateid, uint edgeid)
        {
            var func = m_contract.GetFunction("getTotalRights");
            return await func.CallAsync<uint>(stateid, edgeid);
        }

        public async Task<GetRightOutput> GetRight(uint stateid, uint edgeid, uint rightid)
        {
            var func = m_contract.GetFunction("getRight");
            return await func.CallDeserializingToObjectAsync<GetRightOutput>(stateid, edgeid, rightid);
        }

        public async Task<bool> HasRight(uint state1, uint state2, string user, WFRights right)
        {
            var func = m_contract.GetFunction("hasRight");
            return await func.CallAsync<bool>(state1, state2, user, right);
        }

        public async Task<uint> GetUSN()
        {
            var func = m_contract.GetFunction("usn");
            return await func.CallAsync<uint>();
        }

        public async Task<BigInteger> EstimateAddRight(uint stateid, uint edgeid, string user, WFRights right, EstTyp typ = EstTyp.GAS, BigInteger? gasPrice = null)
        {
            Nethereum.Contracts.Function func = m_contract.GetFunction("addRight");
            var gas = await func.EstimateGasAsync(m_myWallet.Address, null, null, stateid, edgeid, user, right);

            return WF.Estimate(gas, typ, gasPrice);
        }

        public async Task<BigInteger> EstimateRemoveRight(uint stateid, uint edgeid, string user, WFRights right, EstTyp typ = EstTyp.GAS, BigInteger? gasPrice = null)
        {
            Nethereum.Contracts.Function func = m_contract.GetFunction("removeRight");
            var gas = await func.EstimateGasAsync(m_myWallet.Address, null, null, stateid, edgeid, user, right);

            return WF.Estimate(gas, typ, gasPrice);
        }

        public async Task<string> AddRight(uint stateid, uint edgeid, string user, WFRights right, BigInteger? gasPrice = null)
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

        public async Task<string> RemoveRight(uint stateid, uint edgeid, string user, WFRights right, BigInteger? gasPrice = null)
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

            uint states = await GetTotalStates();
            for (uint cntStates = 0; cntStates < states; ++cntStates)
            {
                uint edges = await GetTotalEdges(cntStates);
                for (uint cntEdges = 0; cntEdges < edges; ++cntEdges)
                {
                    uint rights = await GetTotalRights(cntStates, cntEdges);
                    for (uint cntRights = 0; cntRights < rights; ++cntRights)
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
