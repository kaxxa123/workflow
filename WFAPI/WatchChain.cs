using System;
using System.Threading;
using System.Threading.Tasks;
using System.Numerics;
using System.Collections.Concurrent;
using Nethereum.JsonRpc.WebSocketStreamingClient;
using Nethereum.RPC.Reactive.Eth.Subscriptions;
using Nethereum.RPC.Eth.DTOs;

namespace WFApi
{
    using RecptDbl = Tuple<string, BigInteger>;

    //Encapsulates a bunch of properties describing a block.
    //This is passed to the observer instance by CWatchChain::CountBlocks()
    public class CWFBlock
    {
        //The Block number
        public long BlkNum { get; set;  }
        //The Block hash
        public string Hash { get; set; }
    }

    //Encapsulates a bunch of properties describing a transaction. 
    //This is returned by CWatchChain::GetTransaction()
    public class CWFTransaction
    {
        //Transaction sender address
        public string From { get; set; }
        //Transaction recipient address
        public string To { get; set; }
        //Transaction Value sent to recipient in Wei
        public BigInteger Value { get; set; }
        //Transaction Fee in Gas
        public BigInteger Gas { get; set; }
        //Transaction Gas Price
        public BigInteger GasPrice { get; set; }
        //Block Number where this transaction is included
        public BigInteger BlockNumber { get; set; }
        //Block Hash where this transaction is included
        public string BlockHash { get; set; }
    }

    internal class TxFeeHistory
    {
        //The last cache position to which an item was saved.
        private static int m_last = 0;

        //Mapping of type
        // [index] => [Transaction Recpt]
        private static ConcurrentDictionary<int, RecptDbl> m_recpts = new ConcurrentDictionary<int, RecptDbl>();

        //Cache size
        internal static int Size { get; set; } = 50;

        //Delete cache items with postition <= cutOff
        internal static void TrimSize(int cutOff)
        {
            RecptDbl value;
            while ((cutOff > 0) && m_recpts.TryRemove(cutOff, out value))
                --cutOff;
        }

        //Add item to the next cache position
        internal static void Add(string hash, BigInteger fee)
        {
            // Reserve position for the new item and add it
            // Note that at the cache head we may have gaps.
            // Here Thread 3 adds the entry before thread 1 and 2
            //
            // Thread 3                                     [ ]
            // Thread 2                                 [ ]  
            // Thread 1                             [ ]     
            // Cache        [ ] [ ] [ ] [ ] [ ] [ ] - - - - [ ]
            //           0   1   2   3   4   5   6   7   8   9

            int pos = Interlocked.Increment(ref m_last);
            m_recpts[pos] = new RecptDbl(hash.ToUpper(), fee);

            //Given the item position, try deleting any items falling of 
            //the cache size limit
            TrimSize(pos - Size);
        }

        internal static BigInteger? Find(string hash)
        {
            int iEnd   = m_last;
            int iStart = (iEnd > Size) ? (iEnd - Size) : 1;

            hash = hash.ToUpper();

            // Search for the Receipt taking into account that the cache
            // may contain gaps due to concurrent addition.
            RecptDbl value;
            for (; iStart <= iEnd; ++iStart)
                if (m_recpts.TryGetValue(iStart, out value) && (value.Item1 == hash))
                    return value.Item2;

            return null;
        }
    }

    //A number of helpers for retrieving blockchain information
    public class CWatchChain
    {
        //Get transaction information for the given transaction hash
        public static async Task<CWFTransaction> GetTransaction(WFWallet wallet, string sHash)
        {
            Transaction trn = await wallet.W3.Eth.Transactions.GetTransactionByHash.SendRequestAsync(sHash);

            CWFTransaction trnOut = new CWFTransaction();
            trnOut.From = trn.From;
            trnOut.To = trn.To;
            trnOut.Value = trn.Value.Value;
            trnOut.Gas = trn.Gas.Value;
            trnOut.GasPrice = trn.GasPrice.Value;
            trnOut.BlockNumber = trn.BlockNumber.Value;
            trnOut.BlockHash = trn.BlockHash;

            return trnOut;
        }

        //Get transaction fee in Wei for the given transaction hash
        public static async Task<BigInteger> GetTxFee(WFWallet wallet, string sHash)
        {
            BigInteger? fee = TxFeeHistory.Find(sHash);
            if (fee != null)
                return (BigInteger)fee;

            Transaction trn = await wallet.W3.Eth.Transactions.GetTransactionByHash.SendRequestAsync(sHash);
            TransactionReceipt recpt = await wallet.W3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(sHash);
            return recpt.GasUsed.Value * trn.GasPrice.Value;
        }

        //Observe the blockchain for mined blocks
        public static async Task<long> CountBlocks(int iBlkTot, IObserver<CWFBlock> obsrv = null, Func<bool> UserExit = null, string url = "ws://127.0.0.1:8545")
        {
            bool subscribed = true;
            long lastBlockNum = 0;
            int iBlkCnt = 0;

            using (var client = new StreamingWebSocketClient(url))
            {
                //
                // Subscription setup
                var subscription = new EthNewBlockHeadersObservableSubscription(client);

                subscription.GetSubscriptionDataResponsesAsObservable().Subscribe(
                    //IObservable::OnNext
                    (block) => {
                        if (block != null) {
                            Interlocked.Increment(ref iBlkCnt);
                            Interlocked.Exchange(ref lastBlockNum, (long)block.Number.Value);
                            obsrv.OnNext(new CWFBlock() { BlkNum = (long)block.Number.Value, Hash = block.BlockHash });
                        }
                    },
                    //IObservable::OnError
                    (ex) => {
                        subscribed = false;
                        obsrv.OnError(ex);
                    },
                    //IObservable::OnCompleted
                    () => {
                        subscribed = false;
                        obsrv.OnCompleted();
                    }
                );
                // Subscription setup Complete
                //

                // Run Subscription
                // 1. Open the websocket connection
                // 2. Start the subscription
                await client.StartAsync();
                await subscription.SubscribeAsync();

                // 3. Wait until user wants to quit
                while (subscribed && (iBlkCnt < iBlkTot) && (UserExit?.Invoke() != true))
                    await Task.Delay(TimeSpan.FromSeconds(1));

                // 4. Unsubscribe
                if (subscribed)
                    await subscription.UnsubscribeAsync();

                //5. Wait for Unsubscribe to complete
                while (subscribed)
                    await Task.Delay(TimeSpan.FromSeconds(1));
            }

            return lastBlockNum;
        }
    }
}
