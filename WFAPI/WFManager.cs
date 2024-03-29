﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.Hex.HexTypes;

namespace WFApi
{
    //Flags applicable to each document type within the document-set
    public enum WFFlags 
    {
        REQUIRED    = 1,    //Document is required
        PUBLIC      = 2     //Document is to be published
    };

    // Stores a set of properties for the documents that will be traversing the WF
    public struct DocSet
    {
        public uint flags;
        public uint hiLimit;
        public uint loLimit; 
    };

    // Encapsulates the result of a ReadWFs function call
    [FunctionOutput]
    public class ReadWFOutput : IFunctionOutputDTO
    {
        // A list of addresses returned by the ReadWFs function call. 
        // If no addresses are read an empty list is returned.
        [Parameter("address[]", "addrList", 1)]
        public List<string> addrList { get; set; }

        //The id of the next item to be used when reading the list in batches. 
        //If the read operation hits the list end next is set to zero.
        [Parameter("uint256", "next", 2)]
        public uint next { get; set; }
    }

    [Event("EventWFAdded")]
    internal class WFAddedEvent : IEventDTO
    {
        [Parameter("uint256", "id", 1, true)]
        public uint id { get; set; }

        [Parameter("address", "addr", 2, false)]
        public string addr { get; set; }
    }

    [Event("EventWFDeleted")]
    internal class WFDeletedEvent : IEventDTO
    {
        [Parameter("uint256", "id", 1, true)]
        public uint id { get; set; }

        [Parameter("address", "addr", 2, false)]
        public string addr { get; set; }
    }

    //Manages the list of open and closed workflows
    public class WFManager
    {
        protected WFWallet m_myWallet;
        protected Nethereum.Contracts.Contract m_contract;

        public WFManager(WFWallet wallet)
        {
            m_myWallet = wallet;
            m_contract = wallet.W3.Eth.GetContract(WF.MANAGER_ABI, WF.MANAGER_ADDR);
        }

        // Get the total count of open workflows.
        public async Task<uint> GetTotalOpenWFs()
        {
            var func = m_contract.GetFunction("totalWFs");
            return await func.CallAsync<uint>();
        }

        // Get the id of the first open workflow.
        public async Task<uint> GetFirstWF()
        {
            var func = m_contract.GetFunction("firstWF");
            return await func.CallAsync<uint>();
        }

        // Id of next WF to be created, Ids are assigned sequentially.
        public async Task<uint> GetNextWF()
        {
            var func = m_contract.GetFunction("nextWF");
            return await func.CallAsync<uint>();
        }

        // Get the contract Unique Sequence Number.
        public async Task<uint> GetUSN()
        {
            uint one = await GetNextWF();
            uint two = await GetTotalClosedWFs();

            return (two << 32) | one;
        }

        // Reads up to toRead WFs starting from the element with id start.
        public async Task<ReadWFOutput> ReadWFs(uint start, uint toRead)
        {
            var func = m_contract.GetFunction("readWF");
            return await func.CallDeserializingToObjectAsync<ReadWFOutput>(start, toRead);
        }

        // Retrieve transaction fee estimate to create a new workflow.
        public async Task<BigInteger> Estimate(DocSet[] docs, EstTyp typ = EstTyp.GAS, BigInteger? gasPrice = null)
        {
            BigInteger[] docs2 = ConvertDocs(docs);

            Nethereum.Contracts.Function func = m_contract.GetFunction("addWF");
            var gas = await func.EstimateGasAsync(m_myWallet.Address, null, null, WF.BUILDER_ADDR, docs2);

            return WF.Estimate(gas, typ, gasPrice);
        }

        // Create a new workflow with the specified document set.
        public async Task<Tuple<string,string>> AddWF(DocSet[] docs, BigInteger? gasPrice = null)
        {
            BigInteger[] docs2 = ConvertDocs(docs);

            Nethereum.Contracts.Function func = m_contract.GetFunction("addWF");
            var gas = await func.EstimateGasAsync(m_myWallet.Address, null, null, WF.BUILDER_ADDR, docs2);
            var weiGP = WF.FinalGasPrice(gasPrice);
            var recpt = await func.SendTransactionAndWaitForReceiptAsync(
                                        m_myWallet.Address, gas, weiGP, null, null,
                                        WF.BUILDER_ADDR, docs2);

            if ((recpt == null) || (recpt.Status.Value == 0))
                return null;

            var eventList = recpt.DecodeAllEvents<WFAddedEvent>();
            if (eventList.Count != 1)
                return null;

            TxFeeHistory.Add(recpt.TransactionHash, recpt.GasUsed.Value * weiGP.Value);
            return new Tuple<string, string>(eventList[0].Event.addr, recpt.TransactionHash);
        }

        // Get the total count of closed workflows.
        public async Task<uint> GetTotalClosedWFs()
        {
            var func = m_contract.GetFunction("totalClosedWFs");
            return await func.CallAsync<uint>();
        }

        // Get the address of the closed workflow at the specified index.
        public async Task<string> GetClosedWF(uint idx)
        {
            var func = m_contract.GetFunction("closedWFs");
            return await func.CallAsync<string>(idx);
        }

        protected BigInteger[] ConvertDocs(DocSet[] docs)
        {
            BigInteger[] docs2 = new BigInteger[docs.Length];

            for (uint uCnt = 0; uCnt < docs.Length; ++uCnt)
            {
                docs2[uCnt] = docs[uCnt].flags;
                docs2[uCnt] = docs2[uCnt] << 32;
                docs2[uCnt] += docs[uCnt].hiLimit;
                docs2[uCnt] = docs2[uCnt] << 32;
                docs2[uCnt] += docs[uCnt].loLimit;
            }

            return docs2;
        }
    }
}
