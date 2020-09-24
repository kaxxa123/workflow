﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Numerics;
using System.IO;
using Newtonsoft.Json;
using WFApi;

namespace WFTest
{
    class CfgData
    {
        public string infuraKey { get; set; } = null;
    }

    class Program
    {
        //THIS ACCOUNT IS LOADED WITH 1ETH on the Ropsten Test Network. Use with care!
        const string MY_MNEMO_ADDR1     = "security crime belt rib ball crystal upgrade bike subway penalty ability bird";
        const string MY_ADDR_ADDR1      = "0x256EF9b35afd9b00E5C9660DbBc92243f8940D70";

        //Just another account to test transfering Ether
        const string MY_MNEMO_ADDR2     = "chunk raccoon process bamboo skirt shrimp impact close original hat stay erode";
        const string MY_ADDR_ADDR2      = "0xA67025971D5802a59Eb62C62a1a939401e9a6DeD";

        //The Mnemonic used by the test application
        const string MY_MNEMO    = MY_MNEMO_ADDR1;

        //These values will be set after reading the Infura API key from config.json
        //Make sure to configure config.json correctly for this to work.
        static string HTTPS_URL = null;
        static string WSS_URL   = null;

        public static bool InitConfig()
        {
            try
            {
                string sPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                sPath = Path.GetDirectoryName(sPath);

                string sJson = File.ReadAllText($"{sPath}\\config.json");
                CfgData CFG = JsonConvert.DeserializeObject<CfgData>(sJson);

                if (string.IsNullOrWhiteSpace(CFG?.infuraKey))
                    return false;

                HTTPS_URL = $"https://ropsten.infura.io/v3/{CFG.infuraKey}";
                WSS_URL = $"wss://ropsten.infura.io/ws/v3/{CFG.infuraKey}";
            }
            catch
            {
                return false;
            }

            return true;
        }

        static void Main(string[] args)
        {
            if (!InitConfig())
            { 
                Console.WriteLine("Error: Configure Infura API Key in config.json!");
                return;
            }

            MainTest(false).Wait();
        }

        static async Task MainTest(bool bNew)
        {
            WFWallet myWallet;

            try
            {
                myWallet = await GetWallet(bNew);
                if (bNew) return;
                
                //Transfer 1000 Wei, specifying custom Gas Price of 30 GWei
                await DoTransfer(myWallet, MY_ADDR_ADDR2, 1000, 30 * BigInteger.Pow(10, 9));

                await AddWFBuilderRoles(myWallet);
                await GetWFBuilderRoles(myWallet);
                await RemoveWFBuilderRights(myWallet);
                await AddWFBuilderRights(myWallet);
                await ShowWFBuilder(myWallet);

                await AddWFManagerRoles(myWallet);
                await GetWFManagerRoles(myWallet);
                await NewWF(myWallet);
                await ShowWFManager(myWallet);
                await RunFirstWF(myWallet);

                Console.WriteLine();
                Console.WriteLine("* * * * * * * * * * * * * * * ");
                Console.WriteLine("   Ready Hit Enter to Exit");
                Console.WriteLine();
                Console.ReadLine();
            }
            catch (Exception Ex)
            {
                WFErr err = WF.GetErr(Ex.Message);
                Console.WriteLine($"Ending with error: {Ex.Message} ({Enum.GetName(typeof(WFErr), err)})");
            }
        }

        static async Task<WFWallet> GetWallet(bool bNew)
        {
            WFWallet myWallet;

            if (bNew)
            {
                string sMnemo;
                myWallet = WFWallet.CreateWallet(null, out sMnemo, HTTPS_URL);
                if (myWallet == null) return null;

                Console.WriteLine($"Created OK!");
                Console.WriteLine($"Wallet Address: {myWallet.Address}");
                Console.WriteLine($"Wallet Mnemo: {sMnemo}");
                Console.WriteLine();
            }
            else
            {
                myWallet = WFWallet.InitWallet(null, MY_MNEMO, HTTPS_URL);
                if (myWallet == null) return null;

                Console.WriteLine($"Initialized OK!");
                Console.WriteLine($"Wallet Address: {myWallet.Address}");

                //Get the wallet balance
                BigInteger balanceFrom = await myWallet.CheckFunds(null);
                Console.WriteLine($"Balance: {balanceFrom}");
                Console.WriteLine();
            }

            return myWallet;
        }

        static async Task ConfirmTrn(WFWallet myWallet, string hash = null, int iBlks = 2)
        {
            Console.WriteLine($"Watching {iBlks} Blocks, press Q to abort");

            CWFTransaction trnBefore;
            CWFTransaction trnAfter;

            do
            {
                trnBefore = await CWatchChain.GetTransaction(myWallet, hash);
                trnAfter = trnBefore;

                CObserverBlocks obsrv = new CObserverBlocks();
                await CWatchChain.CountBlocks(iBlks, obsrv, obsrv.UserExit, WSS_URL);

                if (hash != null)
                {
                    trnAfter = await CWatchChain.GetTransaction(myWallet, hash);

                    if (trnAfter?.From?.ToUpper() != myWallet.Address.ToUpper())
                        throw new Exception("Transaction Confirmation failed!");
                }

            } while (trnBefore.BlockNumber != trnAfter.BlockNumber);

            Console.WriteLine();
        }

        static async Task<BigInteger> TraceTransaction(WFWallet myWallet, string hash)
        {
            CWFTransaction trn = await CWatchChain.GetTransaction(myWallet, hash);
            BigInteger feeTrn = trn.Gas * trn.GasPrice;
            Console.WriteLine();
            Console.WriteLine($"Block {trn.BlockNumber}. {trn.BlockHash}");
            Console.WriteLine($"[{trn.From}] -> [{trn.To}]");
            Console.WriteLine($"Value/GasUsed/GasPrice/Fee = {trn.Value}/{trn.Gas}/{trn.GasPrice}/{feeTrn}");

            BigInteger feeRecpt = await CWatchChain.GetTxFee(myWallet, hash);
            Console.WriteLine($"Trn Receipt Fee: {feeRecpt}");
            Console.WriteLine();

            if (feeTrn != feeRecpt)
                throw new Exception("Unexpected: Trn Fee from Tansaction and Receipt DO NOT MATCH!");

            return feeRecpt;
        }

        static async Task<BigInteger> DoTransfer(WFWallet myWallet, string receiverAddr, BigInteger amnt, BigInteger? gasPrice = null)
        {
            //Show funds BEFORE
            string sAddr = myWallet.Address;
            BigInteger balanceFrom = await myWallet.CheckFunds(null);
            BigInteger balanceTo = await myWallet.CheckFunds(receiverAddr);
            Console.WriteLine($"Balance {sAddr}: {balanceFrom}");
            Console.WriteLine($"Balance {receiverAddr}: {balanceTo}");

            //Estimate Fees and Final Balance
            BigInteger gasEstimate = await myWallet.Estimate(receiverAddr, amnt);
            BigInteger feeEstimate = gasEstimate * ((gasPrice != null) ? (BigInteger)gasPrice : WF.DefaultGasPrice());
            BigInteger balanceEstimate = balanceFrom - feeEstimate - amnt;
            Console.WriteLine($"Gas Estimate: {gasEstimate}");
            Console.WriteLine($"Fee Estimate: {feeEstimate}");
            Console.WriteLine($"Balance After Estimate: {balanceEstimate}");
            Console.WriteLine();

            //Transfer
            Console.WriteLine("Transfering - Value: {0} Wei @ Gas Price: {1} Wei", amnt, (gasPrice != null) ? gasPrice.ToString() : "<default>");
            string hash = await myWallet.SendFunds(receiverAddr, amnt, gasPrice);
            await ConfirmTrn(myWallet, hash);

            Console.WriteLine($"Transaction: {hash}");
            BigInteger trnFees = await TraceTransaction(myWallet, hash);
            Console.WriteLine();

            //Show funds AFTER
            balanceFrom = await myWallet.CheckFunds(null);
            balanceTo = await myWallet.CheckFunds(receiverAddr);
            Console.WriteLine($"Balance {sAddr}: {balanceFrom}");
            Console.WriteLine($"Balance {receiverAddr}: {balanceTo}");
            Console.WriteLine();

            if (feeEstimate != trnFees)
                throw new Exception("Actual/Estimate Fees mismatch");

            if (balanceFrom != balanceEstimate)
                throw new Exception("Actual/Estimate Balance mismatch");

            Console.WriteLine("==========================================");
            Console.WriteLine("DoTransfer Success!");
            Console.WriteLine();

            return balanceFrom;
        }

        static async Task GetWFBuilderRoles(WFWallet myWallet)
        {
            WFBuilderAccess wfb = new WFBuilderAccess(myWallet);

            uint tot = await wfb.GetAdminCnt(RoleTyp.RootAdmin);
            Console.WriteLine("==========");
            Console.WriteLine("WF Builder");
            Console.WriteLine("==========");
            Console.WriteLine($"Total Root Admins: {tot}");

            for (uint cnt = 0; cnt < tot; ++cnt)
            {
                string sAddr = await wfb.GetAdmin(RoleTyp.RootAdmin, cnt);
                Console.WriteLine($"Admin: {sAddr}");
            }
            Console.WriteLine();

            tot = await wfb.GetAdminCnt(RoleTyp.ContractAdmin);
            Console.WriteLine($"Total Contract Admins: {tot}");

            for (uint cnt = 0; cnt < tot; ++cnt)
            {
                string sAddr = await wfb.GetAdmin(RoleTyp.ContractAdmin, cnt);
                Console.WriteLine($"Admin: {sAddr}");
            }
            Console.WriteLine();
            Console.WriteLine();
        }
        static async Task GetWFManagerRoles(WFWallet myWallet)
        {
            WFManagerAccess wfm = new WFManagerAccess(myWallet);

            uint tot = await wfm.GetAdminCnt(RoleTyp.RootAdmin);
            Console.WriteLine("==========");
            Console.WriteLine("WF Manager");
            Console.WriteLine("==========");
            Console.WriteLine($"Total Root Admins: {tot}");

            for (uint cnt = 0; cnt < tot; ++cnt)
            {
                string sAddr = await wfm.GetAdmin(RoleTyp.RootAdmin, cnt);
                Console.WriteLine($"Admin: {sAddr}");
            }
            Console.WriteLine();

            tot = await wfm.GetAdminCnt(RoleTyp.ContractAdmin);
            Console.WriteLine($"Total Contract Admins: {tot}");

            for (uint cnt = 0; cnt < tot; ++cnt)
            {
                string sAddr = await wfm.GetAdmin(RoleTyp.ContractAdmin, cnt);
                Console.WriteLine($"Admin: {sAddr}");
            }
            Console.WriteLine();
            Console.WriteLine();
        }

        static async Task AddWFBuilderRoles(WFWallet myWallet, bool bFlipFlop = false)
        {
            WFBuilderAccess wfb = new WFBuilderAccess(myWallet);

            Console.WriteLine("==========");
            Console.WriteLine("WF Builder");
            Console.WriteLine("==========");
            Console.WriteLine("Granting Contract Admin Role to Self...");

            //30 GWei
            BigInteger gasPrice = 30 * BigInteger.Pow(10, 9);
            string hash = await wfb.GrantRole(RoleTyp.ContractAdmin, myWallet.Address, gasPrice);
            Console.WriteLine($"Granted OK: {hash}");
            await TraceTransaction(myWallet, hash);
            await ConfirmTrn(myWallet, hash);

            bool bRet = await wfb.HasRole(RoleTyp.RootAdmin, myWallet.Address);
            Console.WriteLine($"Root Admin Role Granted: {bRet}");

            bRet = await wfb.HasRole(RoleTyp.ContractAdmin, myWallet.Address);
            Console.WriteLine($"Contract Admin Role Granted: {bRet}");
            Console.WriteLine();

            if (!bRet)
                throw new Exception("Expected Role NOT Granted");

            if (bFlipFlop)
            {
                Console.WriteLine("Revoking Contract Admin Role to Self...");
                hash = await wfb.RevokeRole(RoleTyp.ContractAdmin, myWallet.Address);
                Console.WriteLine($"Revoked OK: {hash}");
                await TraceTransaction(myWallet, hash);
                await ConfirmTrn(myWallet, hash);

                bRet = await wfb.HasRole(RoleTyp.RootAdmin, myWallet.Address);
                Console.WriteLine($"Root Admin Role Granted: {bRet}");

                bRet = await wfb.HasRole(RoleTyp.ContractAdmin, myWallet.Address);
                Console.WriteLine($"Contract Admin Role Granted: {bRet}");
                Console.WriteLine();

                if (bRet)
                    throw new Exception("Unexpected Role Granted");
            }

            Console.WriteLine("==========================================");
            Console.WriteLine("AddWFBuilderRoles Success!");
            Console.WriteLine();
        }
        static async Task AddWFManagerRoles(WFWallet myWallet, bool bFlipFlop = false)
        {
            WFManagerAccess wfm = new WFManagerAccess(myWallet);

            Console.WriteLine("==========");
            Console.WriteLine("WF Manager");
            Console.WriteLine("==========");
            Console.WriteLine("Granting Contract Admin Role to Self...");
            string hash = await wfm.GrantRole(RoleTyp.ContractAdmin, myWallet.Address);
            await ConfirmTrn(myWallet, hash);

            bool bRet = await wfm.HasRole(RoleTyp.RootAdmin, myWallet.Address);
            Console.WriteLine($"Root Admin Role Granted: {bRet}");

            bRet = await wfm.HasRole(RoleTyp.ContractAdmin, myWallet.Address);
            Console.WriteLine($"Contract Admin Role Granted: {bRet}");
            Console.WriteLine();

            if (!bRet)
                throw new Exception("Expected Role NOT Granted");

            if (bFlipFlop)
            {
                Console.WriteLine("Revoking Contract Admin Role to Self...");

                hash = await wfm.RevokeRole(RoleTyp.ContractAdmin, myWallet.Address);
                await ConfirmTrn(myWallet, hash);

                bRet = await wfm.HasRole(RoleTyp.RootAdmin, myWallet.Address);
                Console.WriteLine($"Root Admin Role Granted: {bRet}");

                bRet = await wfm.HasRole(RoleTyp.ContractAdmin, myWallet.Address);
                Console.WriteLine($"Contract Admin Role Granted: {bRet}");
                Console.WriteLine();

                if (bRet)
                    throw new Exception("Unexpected Role Granted");
            }

            Console.WriteLine("==========================================");
            Console.WriteLine("AddWFManagerRoles Success!");
            Console.WriteLine();
        }

        static async Task AddWFBuilderRights(WFWallet myWallet)
        {
            Console.WriteLine("==================");
            Console.WriteLine("AddWFBuilderRights");
            Console.WriteLine("==================");

            //Assigning ourselves rights on every State Engine Edge
            WFBuilder wfb = new WFBuilder(myWallet);

            string hash = await wfb.AddRight(0, 0, myWallet.Address, WFRights.INIT);
            await ConfirmTrn(myWallet, hash);

            hash = await wfb.AddRight(1, 0, myWallet.Address, WFRights.APPROVE);
            await ConfirmTrn(myWallet, hash);
            hash = await wfb.AddRight(1, 1, myWallet.Address, WFRights.ABORT);
            await ConfirmTrn(myWallet, hash);

            hash = await wfb.AddRight(2, 0, myWallet.Address, WFRights.REVIEW);
            await ConfirmTrn(myWallet, hash);
            hash = await wfb.AddRight(2, 1, myWallet.Address, WFRights.APPROVE);
            await ConfirmTrn(myWallet, hash);
            hash = await wfb.AddRight(2, 2, myWallet.Address, WFRights.ABORT);
            await ConfirmTrn(myWallet, hash);

            hash = await wfb.AddRight(3, 0, myWallet.Address, WFRights.APPROVE);
            await ConfirmTrn(myWallet, hash);
            hash = await wfb.AddRight(3, 1, myWallet.Address, WFRights.SIGNOFF);
            await ConfirmTrn(myWallet, hash);

            hash = await wfb.AddRight(3, 2, myWallet.Address, WFRights.ABORT);
            await ConfirmTrn(myWallet, hash);

            Console.WriteLine("==========================================");
            Console.WriteLine("AddWFBuilderRights Success!");
            Console.WriteLine();
        }
        static async Task RemoveWFBuilderRights(WFWallet myWallet)
        {
            Console.WriteLine("=====================");
            Console.WriteLine("RemoveWFBuilderRights");
            Console.WriteLine("=====================");

            //Removing our rights from every State Engine Edge
            WFBuilder wfb = new WFBuilder(myWallet);

            //This little test is a quick and dirty to verify if the rights were assigned
            //In practice this is not the correct way to do it. Just before the first edge
            //doesn't have any rights it does not mean that all other edges are free of 
            //any rights configuration.
            uint uTotRights = await wfb.GetTotalRights(0, 0);
            if (uTotRights == 0)
            {
                Console.WriteLine("Rights not configured!");
                Console.WriteLine();
                return;
            }

            string hash = await wfb.RemoveRight(0, 0, myWallet.Address, WFRights.INIT);
            await ConfirmTrn(myWallet, hash);

            hash = await wfb.RemoveRight(1, 0, myWallet.Address, WFRights.APPROVE);
            await ConfirmTrn(myWallet, hash);
            hash = await wfb.RemoveRight(1, 1, myWallet.Address, WFRights.ABORT);
            await ConfirmTrn(myWallet, hash);

            hash = await wfb.RemoveRight(2, 0, myWallet.Address, WFRights.REVIEW);
            await ConfirmTrn(myWallet, hash);
            hash = await wfb.RemoveRight(2, 1, myWallet.Address, WFRights.APPROVE);
            await ConfirmTrn(myWallet, hash);
            hash = await wfb.RemoveRight(2, 2, myWallet.Address, WFRights.ABORT);
            await ConfirmTrn(myWallet, hash);

            hash = await wfb.RemoveRight(3, 0, myWallet.Address, WFRights.APPROVE);
            await ConfirmTrn(myWallet, hash);
            hash = await wfb.RemoveRight(3, 1, myWallet.Address, WFRights.SIGNOFF);
            await ConfirmTrn(myWallet, hash);
            hash = await wfb.RemoveRight(3, 2, myWallet.Address, WFRights.ABORT);
            await ConfirmTrn(myWallet, hash);

            Console.WriteLine("==========================================");
            Console.WriteLine("RemoveWFBuilderRights Success!");
            Console.WriteLine();
        }

        static async Task ShowWFBuilder(WFWallet myWallet)
        {
            WFBuilder wfb = new WFBuilder(myWallet);

            BigInteger usn = await wfb.GetUSN();
            uint states = await wfb.GetTotalStates();

            Console.WriteLine($"USN:          {usn}");
            Console.WriteLine($"States Total: {states}");

            for (uint cntStates = 0; cntStates < states; ++cntStates)
            {
                uint edges = await wfb.GetTotalEdges(cntStates);
                Console.WriteLine();
                Console.WriteLine($"Edge Total: {cntStates}/{edges}");

                for (uint cntEdges = 0; cntEdges < edges; ++cntEdges)
                {
                    uint rights = await wfb.GetTotalRights(cntStates, cntEdges);
                    Console.WriteLine($"Right Total: {cntStates}/{cntEdges}/{rights}");

                    for (uint cntRights = 0; cntRights < rights; ++cntRights)
                    {
                        GetRightOutput rightInfo = await wfb.GetRight(cntStates, cntEdges, cntRights);
                        Console.WriteLine($"{cntRights}. {rightInfo.user}:{Enum.GetName(typeof(WFRights), rightInfo.right)}");
                    }
                }
            }
            Console.WriteLine();
            Console.WriteLine();

            for (uint cntStates = 0; cntStates < states; ++cntStates)
            {
                uint edges = await wfb.GetTotalEdges(cntStates);
                for (uint cntEdges = 0; cntEdges < edges; ++cntEdges)
                {
                    uint endState = await wfb.GetEndState(cntStates, cntEdges);
                    Console.WriteLine($"[{cntStates}] -> [{endState}]");
                }
                Console.WriteLine();
            }

            Console.WriteLine();
            Console.WriteLine("Participants:");
            List<string> participants = await wfb.GetParticipants();

            if (participants.Count != 1)
                throw new Exception($"Unexpected # of participants: {participants.Count} instead of 3!");

            foreach (string user in participants)
                Console.WriteLine($"{user}");

            Console.WriteLine("==========================================");
            Console.WriteLine("ShowWFBuilder Success!");
            Console.WriteLine();
        }

        static async Task ShowWFManager(WFWallet myWallet)
        {
            WFManager wfm = new WFManager(myWallet);

            uint totalWF = await wfm.GetTotalOpenWFs();
            uint firstWF = await wfm.GetFirstWF();
            uint nextWF = await wfm.GetNextWF();
            uint usn = await wfm.GetUSN();
            uint totalWFClosed = await wfm.GetTotalClosedWFs();
            ReadWFOutput read = await wfm.ReadWFs(0, 0);
            Console.WriteLine($"USN:            {usn}");
            Console.WriteLine($"Total Open WFs: {totalWF}");
            Console.WriteLine($"First Open WF:  {firstWF}");
            Console.WriteLine($"Total Read WFs: {read.addrList.Count}");
            Console.WriteLine($"Next Read WFs:  {read.next}");
            Console.WriteLine($"Next Id on Adding new WF:   {nextWF}");

            foreach (string addr in read.addrList)
            {
                Console.WriteLine();
                Console.WriteLine($"Workflow Address: {addr}");
                await ShowWorkflow(myWallet, addr);
            }

            Console.WriteLine();
            Console.WriteLine($"Total Closed WFs: {totalWFClosed}");

            for (uint cnt = 0; cnt < totalWFClosed; ++cnt)
            {
                string addrClsd = await wfm.GetClosedWF(cnt);
                Console.WriteLine($"Workflow Address: {addrClsd}");
            }

            Console.WriteLine("==========================================");
            Console.WriteLine("ShowWFManager Success!");
            Console.WriteLine();
        }

        static async Task NewWF(WFWallet myWallet)
        {
            DocSet[] docs = new DocSet[] {  new DocSet { loLimit = 1, hiLimit = 1, flags = (uint)WFFlags.REQUIRED },
                                            new DocSet { loLimit = 1, hiLimit = 2, flags = (uint)WFFlags.REQUIRED },
                                            new DocSet { loLimit = 1, hiLimit = 2, flags = 0 },
                                            new DocSet { loLimit = 2, hiLimit = 2, flags = 0 }};

            BigInteger gasPrice = 30 * BigInteger.Pow(10, 9);
            BigInteger balanceBefore = await myWallet.CheckFunds(null);

            WFManager wfm = new WFManager(myWallet);
            Tuple<string, string> newWf = await wfm.AddWF(docs, gasPrice);
            if (newWf?.Item1 == null)
            {
                Console.WriteLine("Unexpected: No Wf returned.");
                return;
            }
            await ConfirmTrn(myWallet, newWf.Item2);

            BigInteger balanceAfter = await myWallet.CheckFunds(null);
            BigInteger change = balanceBefore - balanceAfter;
            Console.WriteLine($"Balance Before: {balanceBefore}");
            Console.WriteLine($"Balance After:  {balanceAfter}");
            Console.WriteLine($"Balance Change: {change}");

            Console.WriteLine($"WF created at: {newWf.Item1}");
            Console.WriteLine($"Transaction: {newWf.Item2}");
            Console.WriteLine();
            BigInteger feeRecpt = await TraceTransaction(myWallet, newWf.Item2);
            Console.WriteLine();

            if (change != feeRecpt)
                throw new Exception("Unexpected: Balance Change does not match Receipt Fee!");

            Console.WriteLine("==========================================");
            Console.WriteLine("NewWF Success!");
            Console.WriteLine();
        }

        static async Task ShowWorkflow(WFWallet myWallet, string addr)
        {
            Workflow wf = new Workflow(myWallet, addr);

            uint state = await wf.GetState();
            WFMode mode = await wf.GetMode();
            uint uid = await wf.GetID();
            string engine = await wf.GetStateEngine();
            uint totDocTypes = await wf.GetTotalDocTypes();
            uint totHistory = await wf.GetTotalHistory();

            Console.WriteLine($"State:  {state}");
            Console.WriteLine($"Mode:   {Enum.GetName(typeof(WFMode), mode)}");
            Console.WriteLine($"ID:     {uid}");
            Console.WriteLine($"Engine: {engine}");
            Console.WriteLine();
            Console.WriteLine($"Total Doc Types: {totDocTypes}");

            for (uint cntDT = 0; cntDT < totDocTypes; ++cntDT)
            {
                DocTypeInfo typeInfo = await wf.GetDocProps(cntDT);
                Console.WriteLine($"Flags: {typeInfo.flags}, Range: {typeInfo.loLimit} -> {typeInfo.hiLimit}, Count {typeInfo.count}");
            }
            Console.WriteLine();

            Console.WriteLine($"Total History: {totHistory}");
            for (uint cntHist = 0; cntHist < totHistory; ++cntHist)
            {
                DocHistory history = await wf.GetHistory(cntHist);
                Console.WriteLine($"User: {history.user}, Action: {history.action}, New State {history.stateNow}");
                Console.WriteLine($"Total Removed: {history.idsRmv.Count}");

                if (history.idsRmv.Count > 0)
                {
                    for (int cntRmv = 0; cntRmv < history.idsRmv.Count; ++cntRmv)
                        Console.Write("0x{0} ", history.idsRmv[cntRmv].ToString("X"));
                    Console.WriteLine();
                }

                Console.WriteLine($"Total Added: {history.idsAdd.Count}");
                for (int cntAdd = 0; cntAdd < history.idsAdd.Count; ++cntAdd)
                    Console.WriteLine("0x{0} -> 0x{1}", history.idsAdd[cntAdd].ToString("X"), history.contentAdd[cntAdd].ToString("X"));
                Console.WriteLine();
            }
            Console.WriteLine();

            List<BigInteger> latest = await wf.GetLatest(0, totHistory, new List<BigInteger>());
            Console.WriteLine($"Total Latest Documents: {latest.Count}");
            foreach (BigInteger docId in latest)
            {
                var hash = await wf.LatestHash(docId);
                Console.WriteLine("0x{0} => 0x{1}", docId.ToString("X"), hash.ToString("X"));
            }
        }

        static async Task RunWorkflow(WFWallet myWallet, string addr)
        {
            Workflow wf = new Workflow(myWallet, addr);

            BigInteger[] ids = new BigInteger[] { wf.MakeDocId(0, 0x1000), wf.MakeDocId(1, 0x1001) };
            BigInteger[] content = new BigInteger[] { 0x111, 0x112 };

            string hash = await wf.DoInit(0, 1, ids, content);
            await ConfirmTrn(myWallet, hash);

            hash = await wf.DoApprove(1, 2);
            await ConfirmTrn(myWallet, hash);

            hash = await wf.DoApprove(2, 3);
            await ConfirmTrn(myWallet, hash);

            hash = await wf.DoApprove(3, 2);
            await ConfirmTrn(myWallet, hash);

            BigInteger[] idsRemove = new BigInteger[] { wf.MakeDocId(1, 0x1001) };
            BigInteger[] idsAdd = new BigInteger[] { wf.MakeDocId(1, 0x2000), wf.MakeDocId(1, 0x2001) };
            BigInteger[] contentAdd = new BigInteger[] { 0xA111, 0xA112 };
            hash = await wf.DoReview(4, idsRemove, idsAdd, contentAdd);
            await ConfirmTrn(myWallet, hash);

            idsRemove = new BigInteger[0];
            idsAdd = new BigInteger[] { wf.MakeDocId(2, 0x333) };
            contentAdd = new BigInteger[] { 0x333 };
            hash = await wf.DoReview(5, idsRemove, idsAdd, contentAdd);
            await ConfirmTrn(myWallet, hash);

            idsAdd = new BigInteger[] { wf.MakeDocId(3, 0x444), wf.MakeDocId(3, 0x555) };
            contentAdd = new BigInteger[] { 0x444, 0x555 };
            hash = await wf.DoReview(6, idsRemove, idsAdd, contentAdd);
            await ConfirmTrn(myWallet, hash);

            idsRemove = new BigInteger[] { wf.MakeDocId(2, 0x333), wf.MakeDocId(1, 0x2001) };
            idsAdd = new BigInteger[0];
            contentAdd = new BigInteger[0];
            hash = await wf.DoReview(7, idsRemove, idsAdd, contentAdd);
            await ConfirmTrn(myWallet, hash);

            hash = await wf.DoApprove(8, 3);
            await ConfirmTrn(myWallet, hash);

            hash = await wf.DoSignoff(9, 4);
            await ConfirmTrn(myWallet, hash);
        }

        static async Task RunFirstWF(WFWallet myWallet)
        {
            WFManager wfm = new WFManager(myWallet);
            ReadWFOutput read = await wfm.ReadWFs(0, 1);
            if (read.addrList.Count != 1)
            {
                Console.WriteLine("No Open Workflows found!");
                return;
            }

            await ShowWorkflow(myWallet, read.addrList[0]);
            await RunWorkflow(myWallet, read.addrList[0]);
            await ShowWorkflow(myWallet, read.addrList[0]);
        }
    }

    class CObserverBlocks : IObserver<CWFBlock>
    {
        public bool UserExit()
        {
            if (Console.KeyAvailable)
            {
                ConsoleKeyInfo key = Console.ReadKey(true);
                if ((key.Key == ConsoleKey.Q) && ((key.Modifiers == 0) || (key.Modifiers == ConsoleModifiers.Shift)))
                {
                    Console.WriteLine("Aborting...");
                    return true;
                }
            }

            return false;
        }

        void IObserver<CWFBlock>.OnCompleted() { Console.WriteLine($"Data Observable - Completed"); }
        void IObserver<CWFBlock>.OnError(Exception ex) { Console.WriteLine($"Data Observable - EXCEPTION: {ex.Message}"); }
        void IObserver<CWFBlock>.OnNext(CWFBlock blk) { Console.WriteLine($"Blk Number: {blk.BlkNum}, Hash: {blk.Hash}"); }
    }
}
