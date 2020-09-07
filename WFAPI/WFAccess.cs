﻿using System;
using System.Numerics;
using System.Threading.Tasks;

namespace WFApi
{
    public enum RoleTyp
    {
        RootAdmin,
        ContractAdmin,
    }

    public enum RoleOp
    {
        Grant,
        Revoke,
        Renounce
    }

    public class WFBuilderAccess : WorkflowAccess
    {
        public WFBuilderAccess(WFWallet wallet) : base(wallet, WF.BUILDER_ABI, WF.BUILDER_ADDR, "WF_SCHEMA_ADMIN_ROLE")
        { }
    }

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

        public async Task<bool> HasRole(RoleTyp role, string addr)
        {   return await CheckRole(role, addr); }
        public async Task<string> GrantRole(RoleTyp role, string addr, BigInteger? gasPrice = null)
        {   return await UpdateRole(RoleOp.Grant, role, addr, gasPrice); }
        public async Task<string> RevokeRole(RoleTyp role, string addr, BigInteger? gasPrice = null)
        {   return await UpdateRole(RoleOp.Revoke, role, addr, gasPrice); }
        public async Task<string> RenounceRole(RoleTyp role, string addr, BigInteger? gasPrice = null)
        {   return await UpdateRole(RoleOp.Renounce, role, addr, gasPrice); }

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

        protected async Task<bool>   CheckRole(RoleTyp role, string addr)
        {
            byte[] roleId = await GetRole(role);
            Nethereum.Contracts.Function func = m_contract.GetFunction("hasRole");
            return await func.CallAsync<bool>(roleId, addr);
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
