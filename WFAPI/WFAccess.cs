using System;
using System.Numerics;
using System.Threading.Tasks;

namespace WFApi
{
    //Type of Smart Contract Access Role 
    public enum RoleTyp
    {
        RootAdmin,      // Root Administrator Role
        ContractAdmin,  // Contract Administrator Role
    }

    //Type of Smart Contract Access Operation being performed
    public enum RoleOp
    {
        Grant,      // Grant Role
        Revoke,     // Revoke Role
        Renounce    // Renounce own Role
    }

    //Management of Access roles for the WF Builder
    public class WFBuilderAccess : WorkflowAccess
    {
        public WFBuilderAccess(WFWallet wallet) : base(wallet, WF.BUILDER_ABI, WF.BUILDER_ADDR, "WF_SCHEMA_ADMIN_ROLE")
        { }
    }

    //Management of Access roles for the WF Manager
    public class WFManagerAccess : WorkflowAccess
    {
        public WFManagerAccess(WFWallet wallet) : base(wallet, WF.MANAGER_ABI, WF.MANAGER_ADDR, "WF_ADMIN_ROLE")
        { }
    }

    public class WorkflowAccess
    {
        protected WFWallet m_myWallet;
        protected Nethereum.Contracts.Contract m_contract;
        protected string m_roleName;

        protected byte[] DEFAULT_ADMIN_ROLE = null;
        protected byte[] CONTRACT_ADMIN_ROLE = null;

        public WorkflowAccess(WFWallet wallet, string abi, string sContractAddr, string role)
        {
            m_myWallet = wallet;
            m_contract = wallet.W3.Eth.GetContract(abi, sContractAddr);
            m_roleName = role;
        }

        // Get the number of Admins having the specified role
        public async Task<uint> GetAdminCnt(RoleTyp role)
        {
            byte[] roleId = await GetRole(role);
            var func = m_contract.GetFunction("getRoleMemberCount");
            return await func.CallAsync<uint>(roleId);
        }

        // Get Admin address for given Role and Index
        public async Task<string> GetAdmin(RoleTyp role, uint uIdx)
        {
            byte[] roleId = await GetRole(role);
            var func = m_contract.GetFunction("getRoleMember");
            return await func.CallAsync<string>(roleId, uIdx);
        }

        // Verify if account has the specified role
        public async Task<bool> HasRole(RoleTyp role, string addr)
        {
            byte[] roleId = await GetRole(role);
            var func = m_contract.GetFunction("hasRole");
            return await func.CallAsync<bool>(roleId, addr);
        }

        // Grant the specified role to the account with address addr
        public async Task<string> GrantRole(RoleTyp role, string addr, BigInteger? gasPrice = null)
        {   return await UpdateRole(RoleOp.Grant, role, addr, gasPrice); }

        // Revoke the specified role to the account with address addr
        public async Task<string> RevokeRole(RoleTyp role, string addr, BigInteger? gasPrice = null)
        {   return await UpdateRole(RoleOp.Revoke, role, addr, gasPrice); }

        // Renounce the specified role
        public async Task<string> RenounceRole(RoleTyp role, BigInteger? gasPrice = null)
        {   return await UpdateRole(RoleOp.Renounce, role, m_myWallet.Address, gasPrice); }

        // Retrieve the amount of gas required to perform Grant/Revoke/Renounce operation.
        public async Task<BigInteger> Estimate(RoleOp op, RoleTyp role, string addr, EstTyp typ = EstTyp.GAS, BigInteger? gasPrice = null)
        {
            string sFunc = GetOp(op);
            byte[] roleId = await GetRole(role);

            Nethereum.Contracts.Function func = m_contract.GetFunction(sFunc);
            var gas = await func.EstimateGasAsync(m_myWallet.Address, null, null, roleId, addr);

            return WF.Estimate(gas, typ, gasPrice);
        }

        protected async Task<string> UpdateRole(RoleOp op, RoleTyp role, string addr, BigInteger? gasPrice = null)
        {
            string sFunc = GetOp(op);
            byte[] roleId = await GetRole(role);

            Nethereum.Contracts.Function func = m_contract.GetFunction(sFunc);
            var gas = await func.EstimateGasAsync(m_myWallet.Address, null, null, roleId, addr);
            var weiGP = WF.FinalGasPrice(gasPrice);
            var recpt = await func.SendTransactionAndWaitForReceiptAsync(
                                        m_myWallet.Address, gas, weiGP, null, null,
                                        roleId, addr);

            if ((recpt == null) || (recpt.Status.Value == 0))
                return null;

            TxFeeHistory.Add(recpt.TransactionHash, recpt.GasUsed.Value * weiGP.Value);
            return recpt.TransactionHash;
        }

        protected async Task<byte[]>  GetRole(RoleTyp role)
        {
            if (role == RoleTyp.RootAdmin)
            {
                if (DEFAULT_ADMIN_ROLE == null)
                {
                    var func = m_contract.GetFunction("DEFAULT_ADMIN_ROLE");
                    DEFAULT_ADMIN_ROLE = await func.CallAsync<byte[]>();
                }

                return DEFAULT_ADMIN_ROLE;
            }

            if (CONTRACT_ADMIN_ROLE == null)
            {
                var func = m_contract.GetFunction(m_roleName);
                CONTRACT_ADMIN_ROLE = await func.CallAsync<byte[]>();
            }

            return CONTRACT_ADMIN_ROLE;
        }

        protected string GetOp(RoleOp op)
        {
            switch (op)
            {
                case RoleOp.Grant:  return "grantRole";
                case RoleOp.Revoke: return "revokeRole";
            }
            return "renounceRole";
        }
    }
}
