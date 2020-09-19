using System;
using System.Threading.Tasks;
using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;
using System.Collections.Generic;

namespace WFApi
{
    //Identifies Smart Contract running mode
    public enum WFMode
    {
        UNINIT = 0,     // State Engine Uninitialized
        RUNNING = 1,    // State Engine Running
        COMPLETE = 2,   // State Engine Ended Success
        ABORTED = 3     // State Engine Ended Aborted
    };

    // Encapsulates the result of a GetDocProps() function call.
    [FunctionOutput]
    public class DocTypeInfo : IFunctionOutputDTO
    {
        // Any combination of the flags defined in the WFFlags enum.
        [Parameter("uint32", "flags", 1)]
        public uint flags { get; set; }

        // The minimum number of document instances the WF must include for this DocType.
        [Parameter("int32", "loLimit", 2)]
        public int loLimit { get; set; }

        // The maximum number of document instances the WF must include for this DocType.
        [Parameter("int32", "hiLimit", 3)]
        public int hiLimit { get; set; }

        // The number of documents currently included in the workflow matching this DocType.
        [Parameter("int32", "count", 4)]
        public int count { get; set; }
    };

    // Encapsulates the result of a GetHistory()function call.
    [FunctionOutput]
    public class DocHistory
    {
        // Ethereum address of the Workflow participant that performed the action described by this history item.
        [Parameter("address", "user", 1)]
        public string user { get; set; }

        // Type of WFRights action performed.
        [Parameter("uint256", "action", 2)]
        public uint action { get; set; }

        // The new Workflow state number on performing this action.
        [Parameter("uint32", "stateNow", 3)]
        public uint stateNow { get; set; }

        // List of document ids deleted on performing this action.
        [Parameter("uint256[]", "idsRmv", 4)]
        public List<BigInteger> idsRmv { get; set; }

        // List of document ids added/updated on performing this action.
        [Parameter("uint256[]", "idsAdd", 5)]
        public List<BigInteger> idsAdd { get; set; }

        // List of document hashes added/updated on performing this action.
        [Parameter("uint256[]", "contentAdd", 6)]
        public List<BigInteger> contentAdd { get; set; }
    }

    // Encapsulates a workflow instance.
    public class Workflow
    {
        protected WFWallet m_myWallet;
        protected Nethereum.Contracts.Contract m_contract;

        // Initialize a Workflow instance by attaching to a Workflow smart contract 
        public Workflow(WFWallet wallet, string addr)
        {
            m_myWallet = wallet;
            m_contract = wallet.W3.Eth.GetContract(WF.WORKFLOW_ABI, addr);
        }

        // Gets the current Workflow state number.
        public async Task<uint>       GetState()
        {
            var func = m_contract.GetFunction("state");
            return await func.CallAsync<uint>();
        }

        // Gets the current Workflow mode.
        public async Task<WFMode>       GetMode()
        {
            var func = m_contract.GetFunction("mode");
            uint ret = await func.CallAsync<uint>();
            return (WFMode)ret;
        }

        // Gets the Workflow ID assigned by the Workflow Manager.
        public async Task<uint>         GetID()
        {
            var func = m_contract.GetFunction("wfID");
            return await func.CallAsync<uint>();
        }

        // Gets the Workflow Builder smart contract address whose State Engine this Workflow is following. 
        public async Task<string>       GetStateEngine()
        {
            var func = m_contract.GetFunction("engine");
            return await func.CallAsync<string>();
        }

        // Gets the number of Document Types configured for this workflow. 
        public async Task<uint>         GetTotalDocTypes()
        {
            var func = m_contract.GetFunction("totalDocTypes");
            return await func.CallAsync<uint>();
        }

        // Gets the document type properties for the given docType. 
        public async Task<DocTypeInfo>  GetDocProps(uint docType)
        {
            var func = m_contract.GetFunction("getDocProps");
            return await func.CallDeserializingToObjectAsync<DocTypeInfo>(docType);
        }

        // Get the contract Unique Sequence Number. 
        public async Task<uint>         GetUSN()
        {
            return await GetTotalHistory();
        }

        // Gets the total Workflow History list entries.
        public async Task<uint>         GetTotalHistory()
        {
            var func = m_contract.GetFunction("totalHistory");
            return await func.CallAsync<uint>();
        }

        // Retrieve the Workflow History item with the specified index.
        public async Task<DocHistory>   GetHistory(uint idx)
        {
            var func = m_contract.GetFunction("getHistory");
            return await func.CallDeserializingToObjectAsync<DocHistory>(idx);
        }

        // Retrieve the last recorded hash for a specific document.
        public async Task<BigInteger>   LatestHash(BigInteger id)
        {
            var func = m_contract.GetFunction("latest");
            return await func.CallAsync<BigInteger>(id);
        }

        // Retrieve transaction fee estimate to perform a Workflow Initialization operation.
        public async Task<BigInteger> EstimateInit(uint usn, uint nextState, BigInteger[] ids, BigInteger[] content, EstTyp typ = EstTyp.GAS, BigInteger? gasPrice = null)
        {
            Nethereum.Contracts.Function func = m_contract.GetFunction("doInit");
            var gas = await func.EstimateGasAsync(m_myWallet.Address, null, null, usn, nextState, ids, content);

            return WF.Estimate(gas, typ, gasPrice);
        }

        // Retrieve transaction fee estimate to perform a Workflow Approval operation.
        public async Task<BigInteger> EstimateApprove(uint usn, uint nextState, EstTyp typ = EstTyp.GAS, BigInteger? gasPrice = null)
        {
            Nethereum.Contracts.Function func = m_contract.GetFunction("doApprove");
            var gas = await func.EstimateGasAsync(m_myWallet.Address, null, null, usn, nextState);

            return WF.Estimate(gas, typ, gasPrice);
        }

        // Retrieve transaction fee estimate to perform a Workflow Review operation. 
        public async Task<BigInteger> EstimateReview(uint usn, BigInteger[] idsRmv, BigInteger[] idsAdd, BigInteger[] contentAdd, EstTyp typ = EstTyp.GAS, BigInteger? gasPrice = null)
        {
            Nethereum.Contracts.Function func = m_contract.GetFunction("doReview");
            var gas = await func.EstimateGasAsync(m_myWallet.Address, null, null, usn, idsRmv, idsAdd, contentAdd);

            return WF.Estimate(gas, typ, gasPrice);
        }

        // Retrieve transaction fee estimate to perform a Workflow Sign-off operation. 
        public async Task<BigInteger> EstimateSignoff(uint usn, uint nextState, EstTyp typ = EstTyp.GAS, BigInteger? gasPrice = null)
        {
            Nethereum.Contracts.Function func = m_contract.GetFunction("doSignoff");
            var gas = await func.EstimateGasAsync(m_myWallet.Address, null, null, usn, nextState);

            return WF.Estimate(gas, typ, gasPrice);
        }

        // Retrieve transaction fee estimate to perform a Workflow Abort operation.
        public async Task<BigInteger> EstimateAbort(uint usn, uint nextState, EstTyp typ = EstTyp.GAS, BigInteger? gasPrice = null)
        {
            Nethereum.Contracts.Function func = m_contract.GetFunction("doAbort");
            var gas = await func.EstimateGasAsync(m_myWallet.Address, null, null, usn, nextState);

            return WF.Estimate(gas, typ, gasPrice);
        }

        // Moves the Workflow from the uninitialized state to its first running state.
        public async Task<string> DoInit(uint usn, uint nextState, BigInteger[] ids, BigInteger[] content, BigInteger? gasPrice = null)
        {
            Nethereum.Contracts.Function func = m_contract.GetFunction("doInit");
            var gas = await func.EstimateGasAsync(m_myWallet.Address, null, null, usn, nextState, ids, content);
            var weiGP = WF.FinalGasPrice(gasPrice);
            var recpt = await func.SendTransactionAndWaitForReceiptAsync(
                                        m_myWallet.Address, gas, weiGP, null, null,
                                        usn, nextState, ids, content);

            if ((recpt == null) || (recpt.Status.Value == 0))
                return null;

            TxFeeHistory.Add(recpt.TransactionHash, recpt.GasUsed.Value * weiGP.Value);
            return recpt.TransactionHash;
        }

        // Approves the current documents version, allowing the workflow to move to a next state.
        public async Task<string> DoApprove(uint usn, uint nextState, BigInteger? gasPrice = null)
        {
            Nethereum.Contracts.Function func = m_contract.GetFunction("doApprove");
            var gas = await func.EstimateGasAsync(m_myWallet.Address, null, null, usn, nextState);
            var weiGP = WF.FinalGasPrice(gasPrice);
            var recpt = await func.SendTransactionAndWaitForReceiptAsync(
                                        m_myWallet.Address, gas, weiGP, null, null,
                                        usn, nextState);

            if ((recpt == null) || (recpt.Status.Value == 0))
                return null;

            TxFeeHistory.Add(recpt.TransactionHash, recpt.GasUsed.Value * weiGP.Value);
            return recpt.TransactionHash;
        }

        // Submitting of document updates to the workflow.
        public async Task<string> DoReview(uint usn, BigInteger[] idsRmv, BigInteger[] idsAdd, BigInteger[] contentAdd, BigInteger? gasPrice = null)
        {
            Nethereum.Contracts.Function func = m_contract.GetFunction("doReview");
            var gas = await func.EstimateGasAsync(m_myWallet.Address, null, null, usn, idsRmv, idsAdd, contentAdd);
            var weiGP = WF.FinalGasPrice(gasPrice);
            var recpt = await func.SendTransactionAndWaitForReceiptAsync(
                                        m_myWallet.Address, gas, weiGP, null, null,
                                        usn, idsRmv, idsAdd, contentAdd);

            if ((recpt == null) || (recpt.Status.Value == 0))
                return null;

            TxFeeHistory.Add(recpt.TransactionHash, recpt.GasUsed.Value * weiGP.Value);
            return recpt.TransactionHash;
        }

        // Successfully conclude the Workflow.
        public async Task<string> DoSignoff(uint usn, uint nextState, BigInteger? gasPrice = null)
        {
            Nethereum.Contracts.Function func = m_contract.GetFunction("doSignoff");
            var gas = await func.EstimateGasAsync(m_myWallet.Address, null, null, usn, nextState);
            var weiGP = WF.FinalGasPrice(gasPrice);
            var recpt = await func.SendTransactionAndWaitForReceiptAsync(
                                        m_myWallet.Address, gas, weiGP, null, null,
                                        usn, nextState);

            if ((recpt == null) || (recpt.Status.Value == 0))
                return null;

            TxFeeHistory.Add(recpt.TransactionHash, recpt.GasUsed.Value * weiGP.Value);
            return recpt.TransactionHash;
        }

        // Abort the Workflow.
        public async Task<string> DoAbort(uint usn, uint nextState, BigInteger? gasPrice = null)
        {
            Nethereum.Contracts.Function func = m_contract.GetFunction("doAbort");
            var gas = await func.EstimateGasAsync(m_myWallet.Address, null, null, usn, nextState);
            var weiGP = WF.FinalGasPrice(gasPrice);
            var recpt = await func.SendTransactionAndWaitForReceiptAsync(
                                        m_myWallet.Address, gas, weiGP, null, null,
                                        usn, nextState);

            if ((recpt == null) || (recpt.Status.Value == 0))
                return null;

            TxFeeHistory.Add(recpt.TransactionHash, recpt.GasUsed.Value * weiGP.Value);
            return recpt.TransactionHash;
        }

        // Workout a document id given its document type and a unique number.
        public BigInteger MakeDocId(uint docType, uint id)
        {
            BigInteger num = new BigInteger(id);
            num  = num << 32;
            num += docType;
            return num;
        }

        // Retrieve the list of ids of all the documents submitted to this workflow.
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
