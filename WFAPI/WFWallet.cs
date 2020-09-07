using System;
using System.Threading.Tasks;
using System.Numerics;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Nethereum.HdWallet;
using Nethereum.Util;

namespace WFApi
{
    public class WFWallet
    {
        private Account m_account;
        private Web3 m_web3;

        //This class cannot be instantiated directly
        private WFWallet() { }
        private WFWallet(Web3 w3, Account acc) 
        {
            m_web3 = w3;
            m_account = acc;
        }

        // Description:
        //      Initialize WFWallet by creating a new one
        //
        // Parameters: 
        //      sPassword - Password for protecting key
        //      mnemo     - [out] mnemonic string to be saved for re-initializing wallet
        //      url       - Ethereum node connection URL
        //
        // Return:
        //      New WFWallet instance 
        public static WFWallet CreateWallet(string sPassword, out string mnemo, string url = "http://localhost:8545/") 
        {
            mnemo = null;

            if (String.IsNullOrWhiteSpace(sPassword))
                throw new ArgumentException("Password cannot be empty");

            sPassword = sPassword.Trim();

            var newMnemo = new NBitcoin.Mnemonic(NBitcoin.Wordlist.English, NBitcoin.WordCount.Twelve);
            Wallet wallet = new Wallet(newMnemo.ToString(), sPassword);
            Account acc = wallet.GetAccount(0);
            Web3 web3 = new Web3(acc, url);

            mnemo = newMnemo.ToString();

            return new WFWallet(web3, acc);
        }

        // Description:
        //      Initialize WFWallet from existing mnemonic + password
        //
        // Parameters: 
        //      sPassword - Password for unlocking key
        //      sMnemo    - Mnemonic string retuned when wallet was created.
        //      url       - Ethereum node connection URL
        //
        // Return:
        //      New WFWallet instance 
        public static WFWallet InitWallet(string sPassword, string sMnemo, string url = "http://localhost:8545/")
        {
            if (String.IsNullOrWhiteSpace(sMnemo))
                throw new ArgumentException("Mnemonic cannot be empty");

            if (String.IsNullOrWhiteSpace(sPassword))
                throw new ArgumentException("Password cannot be empty");

            sMnemo = sMnemo.Trim();
            sPassword = sPassword.Trim();

            Wallet wallet = new Wallet(sMnemo, sPassword);
            Account acc = wallet.GetAccount(0);
            Web3 web3 = new Web3(acc, url);

            return new WFWallet(web3, acc);
        }

        // Description:
        //      Get the address for the Wallet to receive funds
        //
        // Return:
        //      Account address string
        public string ReceiveFunds() 
        {
            return m_account.Address;
        }

        // Description:
        //      Send funds to recipients
        //
        // Parameters: 
        //      sAddr - Receiver address
        //      amnt - amount to transfer in Wei
        //      gasPrice - Gas Price in Wei or null
        //
        // Return:
        //      Transaction Hash
        public async Task<string> SendFunds(string sAddr, BigInteger amnt, BigInteger? gasPrice = null) 
        {
            BigInteger weiGP = WF.FinalGasPrice(gasPrice).Value;
            decimal gweiGP  = Web3.Convert.FromWei(weiGP, UnitConversion.EthUnit.Gwei);
            decimal ethAmnt = Web3.Convert.FromWei(amnt);

            var recpt = await m_web3.Eth.GetEtherTransferService()
                                    .TransferEtherAndWaitForReceiptAsync(sAddr, ethAmnt, gweiGP);

            if ((recpt == null) || (recpt.Status.Value == 0))
                return null;

            TxFeeHistory.Add(recpt.TransactionHash, recpt.GasUsed.Value * weiGP);
            return recpt.TransactionHash;
        }

        // Description:
        //      Retrieve balance for own wallet or for another user’s wallet identified by address.
        //
        // Parameters: 
        //      sAddr - Address for account whose balance is to be retreived
        //              set to null for retreiving own balance
        //
        // Return:
        //      Balance in Wei
        public async Task<BigInteger> CheckFunds(string sAddr = null)
        {
            var balance = await m_web3.Eth.GetBalance.SendRequestAsync(sAddr ?? ReceiveFunds());
            return balance.Value;
        }

        // Description:
        //      Retrieve the amount of gas required to perform transfer.
        //
        // Parameters: 
        //      sAddr - Receiver address
        //      amnt - amount to transfer in Wei
        //
        // Return:
        //      Gas Amount Estimate
        public async Task<BigInteger> Estimate(string sAddr, BigInteger amnt, EstTyp typ = EstTyp.GAS, BigInteger? gasPrice = null)
        {
            decimal ethAmnt = Web3.Convert.FromWei(amnt);
            BigInteger gas = await m_web3.Eth.GetEtherTransferService().EstimateGasAsync(sAddr, ethAmnt);

            return WF.Estimate(gas, typ, gasPrice);
        }

        public Web3 W3 { get { return m_web3; } }
        public string Address { get { return m_account?.Address; } }
    }
}
