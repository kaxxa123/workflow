using System;
using System.Threading.Tasks;
using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;
using System.Collections.Generic;

namespace WFApi
{
    public enum WFMode
    {
        UNINIT = 0,    //State Engine Unintialized
        RUNNING = 1,    //State Engine Running
        COMPLETE = 2,    //State Engine Ended Success
        ABORTED = 3     //State Entine Ended Aborted
    };

    [FunctionOutput]
    public class DocTypeInfo : IFunctionOutputDTO
    {
        [Parameter("uint32", "flags", 1)]
        public UInt32 flags { get; set; }

        [Parameter("int32", "loLimit", 2)]
        public Int32 loLimit { get; set; }

        [Parameter("int32", "hiLimit", 3)]
        public Int32 hiLimit { get; set; }

        [Parameter("int32", "count", 4)]
        public Int32 count { get; set; }
    };

    [FunctionOutput]
    public class DocHistory
    {
        [Parameter("address", "user", 1)]
        public string user { get; set; }

        [Parameter("uint256", "action", 2)]
        public uint action { get; set; }

        [Parameter("uint32", "stateNow", 3)]
        public UInt32 stateNow { get; set; }

        [Parameter("uint256[]", "idsRmv", 4)]
        public List<BigInteger> idsRmv { get; set; }

        [Parameter("uint256[]", "idsAdd", 5)]
        public List<BigInteger> idsAdd { get; set; }

        [Parameter("uint256[]", "contentAdd", 6)]
        public List<BigInteger> contentAdd { get; set; }
    }

    public class Workflow
    {
        protected WFWallet m_myWallet;
        protected Nethereum.Contracts.Contract m_contract;

        public Workflow(WFWallet wallet, string addr)
        {
            m_myWallet = wallet;
            m_contract = wallet.W3.Eth.GetContract(WF.WORKFLOW_ABI, addr);
        }

        public async Task<UInt32>       GetState()
        {
            var func = m_contract.GetFunction("state");
            return await func.CallAsync<UInt32>();
        }

        public async Task<WFMode>       GetMode()
        {
            var func = m_contract.GetFunction("mode");
            uint ret = await func.CallAsync<uint>();
            return (WFMode)ret;
        }

        public async Task<uint>         GetID()
        {
            var func = m_contract.GetFunction("wfID");
            return await func.CallAsync<uint>();
        }

        public async Task<string>       GetStateEngine()
        {
            var func = m_contract.GetFunction("engine");
            return await func.CallAsync<string>();
        }

        public async Task<uint>         GetTotalDocTypes()
        {
            var func = m_contract.GetFunction("totalDocTypes");
            return await func.CallAsync<uint>();
        }

        public async Task<DocTypeInfo>  GetDocProps(UInt32 docType)
        {
            var func = m_contract.GetFunction("getDocProps");
            return await func.CallDeserializingToObjectAsync<DocTypeInfo>(docType);
        }

        public async Task<uint>         GetUSN()
        {
            return await GetTotalHistory();
        }

        public async Task<uint>         GetTotalHistory()
        {
            var func = m_contract.GetFunction("totalHistory");
            return await func.CallAsync<uint>();
        }

        public async Task<DocHistory>   GetHistory(uint idx)
        {
            var func = m_contract.GetFunction("getHistory");
            return await func.CallDeserializingToObjectAsync<DocHistory>(idx);
        }

        public async Task<BigInteger>   LatestHash(BigInteger id)
        {
            var func = m_contract.GetFunction("latest");
            return await func.CallAsync<BigInteger>(id);
        }

        public async Task<BigInteger> EstimateInit(UInt32 nextState, BigInteger[] ids, BigInteger[] content, EstTyp typ = EstTyp.GAS, BigInteger? gasPrice = null)
        {
            Nethereum.Contracts.Function func = m_contract.GetFunction("doInit");
            var gas = await func.EstimateGasAsync(m_myWallet.Address, null, null, nextState, ids, content);

            return WF.Estimate(gas, typ, gasPrice);
        }
        public async Task<BigInteger> EstimateApprove(UInt32 nextState, EstTyp typ = EstTyp.GAS, BigInteger? gasPrice = null)
        {
            Nethereum.Contracts.Function func = m_contract.GetFunction("doApprove");
            var gas = await func.EstimateGasAsync(m_myWallet.Address, null, null, nextState);

            return WF.Estimate(gas, typ, gasPrice);
        }
        public async Task<BigInteger> EstimateReview(BigInteger[] idsRmv, BigInteger[] idsAdd, BigInteger[] contentAdd, EstTyp typ = EstTyp.GAS, BigInteger? gasPrice = null)
        {
            Nethereum.Contracts.Function func = m_contract.GetFunction("doReview");
            var gas = await func.EstimateGasAsync(m_myWallet.Address, null, null, idsRmv, idsAdd, contentAdd);

            return WF.Estimate(gas, typ, gasPrice);
        }
        public async Task<BigInteger> EstimateSignoff(UInt32 nextState, EstTyp typ = EstTyp.GAS, BigInteger? gasPrice = null)
        {
            Nethereum.Contracts.Function func = m_contract.GetFunction("doSignoff");
            var gas = await func.EstimateGasAsync(m_myWallet.Address, null, null, nextState);

            return WF.Estimate(gas, typ, gasPrice);
        }
        public async Task<BigInteger> EstimateAbort(UInt32 nextState, EstTyp typ = EstTyp.GAS, BigInteger? gasPrice = null)
        {
            Nethereum.Contracts.Function func = m_contract.GetFunction("doAbort");
            var gas = await func.EstimateGasAsync(m_myWallet.Address, null, null, nextState);

            return WF.Estimate(gas, typ, gasPrice);
        }

        public async Task<string> DoInit(UInt32 nextState, BigInteger[] ids, BigInteger[] content, BigInteger? gasPrice = null)
        {
            Nethereum.Contracts.Function func = m_contract.GetFunction("doInit");
            var gas = await func.EstimateGasAsync(m_myWallet.Address, null, null, nextState, ids, content);
            var weiGP = WF.FinalGasPrice(gasPrice);
            var recpt = await func.SendTransactionAndWaitForReceiptAsync(
                                        m_myWallet.Address, gas, weiGP, null, null,
                                        nextState, ids, content);

            if ((recpt == null) || (recpt.Status.Value == 0))
                return null;

            TxFeeHistory.Add(recpt.TransactionHash, recpt.GasUsed.Value * weiGP.Value);
            return recpt.TransactionHash;
        }

        public async Task<string> DoApprove(UInt32 nextState, BigInteger? gasPrice = null)
        {
            Nethereum.Contracts.Function func = m_contract.GetFunction("doApprove");
            var gas = await func.EstimateGasAsync(m_myWallet.Address, null, null, nextState);
            var weiGP = WF.FinalGasPrice(gasPrice);
            var recpt = await func.SendTransactionAndWaitForReceiptAsync(
                                        m_myWallet.Address, gas, weiGP, null, null,
                                        nextState);

            if ((recpt == null) || (recpt.Status.Value == 0))
                return null;

            TxFeeHistory.Add(recpt.TransactionHash, recpt.GasUsed.Value * weiGP.Value);
            return recpt.TransactionHash;
        }

        public async Task<string> DoReview(BigInteger[] idsRmv, BigInteger[] idsAdd, BigInteger[] contentAdd, BigInteger? gasPrice = null)
        {
            Nethereum.Contracts.Function func = m_contract.GetFunction("doReview");
            var gas = await func.EstimateGasAsync(m_myWallet.Address, null, null, idsRmv, idsAdd, contentAdd);
            var weiGP = WF.FinalGasPrice(gasPrice);
            var recpt = await func.SendTransactionAndWaitForReceiptAsync(
                                        m_myWallet.Address, gas, weiGP, null, null,
                                        idsRmv, idsAdd, contentAdd);

            if ((recpt == null) || (recpt.Status.Value == 0))
                return null;

            TxFeeHistory.Add(recpt.TransactionHash, recpt.GasUsed.Value * weiGP.Value);
            return recpt.TransactionHash;
        }

        public async Task<string> DoSignoff(UInt32 nextState, BigInteger? gasPrice = null)
        {
            Nethereum.Contracts.Function func = m_contract.GetFunction("doSignoff");
            var gas = await func.EstimateGasAsync(m_myWallet.Address, null, null, nextState);
            var weiGP = WF.FinalGasPrice(gasPrice);
            var recpt = await func.SendTransactionAndWaitForReceiptAsync(
                                        m_myWallet.Address, gas, weiGP, null, null,
                                        nextState);

            if ((recpt == null) || (recpt.Status.Value == 0))
                return null;

            TxFeeHistory.Add(recpt.TransactionHash, recpt.GasUsed.Value * weiGP.Value);
            return recpt.TransactionHash;
        }

        public async Task<string> DoAbort(UInt32 nextState, BigInteger? gasPrice = null)
        {
            Nethereum.Contracts.Function func = m_contract.GetFunction("doAbort");
            var gas = await func.EstimateGasAsync(m_myWallet.Address, null, null, nextState);
            var weiGP = WF.FinalGasPrice(gasPrice);
            var recpt = await func.SendTransactionAndWaitForReceiptAsync(
                                        m_myWallet.Address, gas, weiGP, null, null,
                                        nextState);

            if ((recpt == null) || (recpt.Status.Value == 0))
                return null;

            TxFeeHistory.Add(recpt.TransactionHash, recpt.GasUsed.Value * weiGP.Value);
            return recpt.TransactionHash;
        }

        public BigInteger MakeDocId(UInt32 docType, uint id)
        {
            BigInteger num = new BigInteger(id);
            num  = num << 32;
            num += docType;
            return num;
        }

        public async Task<List<BigInteger>> GetLatest(uint start, uint end, List<BigInteger> startList)
        {
            for (uint cnt = start; cnt < end; ++cnt)
            {
                DocHistory hist = await GetHistory(cnt);

                if (hist.action == (uint)WFRights.INIT)
                {
                    foreach (BigInteger id in hist.idsAdd)
                        if (startList.FindIndex(x => x == id) == -1)
                            startList.Add(id);
                }
                else if (hist.action == (uint)WFRights.REVIEW)
                {
                    foreach (BigInteger id in hist.idsRmv)
                        startList.Remove(id);

                    foreach (BigInteger id in hist.idsAdd)
                        if (startList.FindIndex(x => x == id) == -1)
                            startList.Add(id);
                }
            }

            return startList;
        }
    }
}
