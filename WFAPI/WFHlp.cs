using System.Numerics;
using Nethereum.Hex.HexTypes;

namespace WFApi
{
    public enum EstTyp
    {
        GAS,
        WEI,
        EUR
    }

    public class WF
    {
        public const string BUILDER_ABI =
        @"[{""inputs"":[],""stateMutability"":""nonpayable"",""type"":""constructor""},{""anonymous"":false,""inputs"":[{""indexed"":true,""internalType"":""bytes32"",""name"":""role"",""type"":""bytes32""}," +
        @"{""indexed"":true,""internalType"":""bytes32"",""name"":""previousAdminRole"",""type"":""bytes32""},{""indexed"":true,""internalType"":""bytes32"",""name"":""newAdminRole"",""type"":""bytes32""}],""name"":""RoleAdminChanged"",""type"":""event""}," +
        @"{""anonymous"":false,""inputs"":[{""indexed"":true,""internalType"":""bytes32"",""name"":""role"",""type"":""bytes32""},{""indexed"":true,""internalType"":""address"",""name"":""account"",""type"":""address""}," +
        @"{""indexed"":true,""internalType"":""address"",""name"":""sender"",""type"":""address""}],""name"":""RoleGranted"",""type"":""event""},{""anonymous"":false,""inputs"":[{""indexed"":true,""internalType"":""bytes32"",""name"":""role"",""type"":""bytes32""}," +
        @"{""indexed"":true,""internalType"":""address"",""name"":""account"",""type"":""address""},{""indexed"":true,""internalType"":""address"",""name"":""sender"",""type"":""address""}],""name"":""RoleRevoked"",""type"":""event""}," +
        @"{""inputs"":[],""name"":""DEFAULT_ADMIN_ROLE"",""outputs"":[{""internalType"":""bytes32"",""name"":"""",""type"":""bytes32""}],""stateMutability"":""view"",""type"":""function""}," +
        @"{""inputs"":[],""name"":""WF_SCHEMA_ADMIN_ROLE"",""outputs"":[{""internalType"":""bytes32"",""name"":"""",""type"":""bytes32""}],""stateMutability"":""view"",""type"":""function""}," +
        @"{""inputs"":[],""name"":""finalized"",""outputs"":[{""internalType"":""bool"",""name"":"""",""type"":""bool""}],""stateMutability"":""view"",""type"":""function""}," +
        @"{""inputs"":[{""internalType"":""bytes32"",""name"":""role"",""type"":""bytes32""}],""name"":""getRoleAdmin"",""outputs"":[{""internalType"":""bytes32"",""name"":"""",""type"":""bytes32""}],""stateMutability"":""view"",""type"":""function""}," +
        @"{""inputs"":[{""internalType"":""bytes32"",""name"":""role"",""type"":""bytes32""},{""internalType"":""uint256"",""name"":""index"",""type"":""uint256""}],""name"":""getRoleMember"",""outputs"":[{""internalType"":""address"",""name"":"""",""type"":""address""}],""stateMutability"":""view"",""type"":""function""}," +
        @"{""inputs"":[{""internalType"":""bytes32"",""name"":""role"",""type"":""bytes32""}],""name"":""getRoleMemberCount"",""outputs"":[{""internalType"":""uint256"",""name"":"""",""type"":""uint256""}],""stateMutability"":""view"",""type"":""function""}," +
        @"{""inputs"":[{""internalType"":""bytes32"",""name"":""role"",""type"":""bytes32""},{""internalType"":""address"",""name"":""account"",""type"":""address""}],""name"":""grantRole"",""outputs"":[],""stateMutability"":""nonpayable"",""type"":""function""}," +
        @"{""inputs"":[{""internalType"":""bytes32"",""name"":""role"",""type"":""bytes32""},{""internalType"":""address"",""name"":""account"",""type"":""address""}],""name"":""hasRole"",""outputs"":[{""internalType"":""bool"",""name"":"""",""type"":""bool""}],""stateMutability"":""view"",""type"":""function""}," +
        @"{""inputs"":[{""internalType"":""bytes32"",""name"":""role"",""type"":""bytes32""},{""internalType"":""address"",""name"":""account"",""type"":""address""}],""name"":""renounceRole"",""outputs"":[],""stateMutability"":""nonpayable"",""type"":""function""}," +
        @"{""inputs"":[{""internalType"":""bytes32"",""name"":""role"",""type"":""bytes32""},{""internalType"":""address"",""name"":""account"",""type"":""address""}],""name"":""revokeRole"",""outputs"":[],""stateMutability"":""nonpayable"",""type"":""function""}," +
        @"{""inputs"":[{""internalType"":""uint256"",""name"":"""",""type"":""uint256""},{""internalType"":""uint256"",""name"":"""",""type"":""uint256""}],""name"":""rights"",""outputs"":[{""internalType"":""uint256"",""name"":"""",""type"":""uint256""}],""stateMutability"":""view"",""type"":""function""}," +
        @"{""inputs"":[{""internalType"":""uint256"",""name"":"""",""type"":""uint256""},{""internalType"":""uint256"",""name"":"""",""type"":""uint256""}],""name"":""states"",""outputs"":[{""internalType"":""uint32"",""name"":"""",""type"":""uint32""}],""stateMutability"":""view"",""type"":""function""}," +
        @"{""inputs"":[],""name"":""usn"",""outputs"":[{""internalType"":""uint256"",""name"":"""",""type"":""uint256""}],""stateMutability"":""view"",""type"":""function""},{""inputs"":[{""internalType"":""uint32[]"",""name"":""edges"",""type"":""uint32[]""}],""name"":""addState"",""outputs"":[],""stateMutability"":""nonpayable"",""type"":""function""}," +
        @"{""inputs"":[],""name"":""doFinalize"",""outputs"":[],""stateMutability"":""nonpayable"",""type"":""function""},{""inputs"":[{""internalType"":""uint32"",""name"":""stateid"",""type"":""uint32""},{""internalType"":""uint32"",""name"":""edgeid"",""type"":""uint32""}," +
        @"{""internalType"":""address"",""name"":""user"",""type"":""address""},{""internalType"":""enum WFRights"",""name"":""right"",""type"":""uint8""}],""name"":""addRight"",""outputs"":[],""stateMutability"":""nonpayable"",""type"":""function""}," +
        @"{""inputs"":[{""internalType"":""uint32"",""name"":""stateid"",""type"":""uint32""},{""internalType"":""uint32"",""name"":""edgeid"",""type"":""uint32""},{""internalType"":""address"",""name"":""user"",""type"":""address""}," +
        @"{""internalType"":""enum WFRights"",""name"":""right"",""type"":""uint8""}],""name"":""removeRight"",""outputs"":[],""stateMutability"":""nonpayable"",""type"":""function""},{""inputs"":[],""name"":""getTotalStates"",""outputs"":[{""internalType"":""uint32"",""name"":"""",""type"":""uint32""}],""stateMutability"":""view"",""type"":""function""}," +
        @"{""inputs"":[{""internalType"":""uint32"",""name"":""stateid"",""type"":""uint32""}],""name"":""getTotalEdges"",""outputs"":[{""internalType"":""uint32"",""name"":"""",""type"":""uint32""}],""stateMutability"":""view"",""type"":""function""}," +
        @"{""inputs"":[{""internalType"":""uint32"",""name"":""stateid"",""type"":""uint32""},{""internalType"":""uint32"",""name"":""edgeid"",""type"":""uint32""}],""name"":""getTotalRights"",""outputs"":[{""internalType"":""uint32"",""name"":"""",""type"":""uint32""}],""stateMutability"":""view"",""type"":""function""}," +
        @"{""inputs"":[{""internalType"":""uint32"",""name"":""state1"",""type"":""uint32""},{""internalType"":""uint32"",""name"":""state2"",""type"":""uint32""},{""internalType"":""address"",""name"":""user"",""type"":""address""}," +
        @"{""internalType"":""enum WFRights"",""name"":""right"",""type"":""uint8""}],""name"":""hasRight"",""outputs"":[{""internalType"":""bool"",""name"":"""",""type"":""bool""}],""stateMutability"":""view"",""type"":""function""}," +
        @"{""inputs"":[{""internalType"":""uint32"",""name"":""stateid"",""type"":""uint32""},{""internalType"":""uint32"",""name"":""edgeid"",""type"":""uint32""},{""internalType"":""uint32"",""name"":""rightid"",""type"":""uint32""}],""name"":""getRight"",""outputs"":[{""internalType"":""address"",""name"":""user"",""type"":""address""}," +
        @"{""internalType"":""enum WFRights"",""name"":""right"",""type"":""uint8""}],""stateMutability"":""view"",""type"":""function""},{""inputs"":[{""internalType"":""uint32"",""name"":""stateid"",""type"":""uint32""}," +
        @"{""internalType"":""uint32"",""name"":""edgeid"",""type"":""uint32""}],""name"":""makeRightsKey"",""outputs"":[{""internalType"":""uint256"",""name"":"""",""type"":""uint256""}],""stateMutability"":""pure"",""type"":""function""}," +
        @"{""inputs"":[{""internalType"":""address"",""name"":""user"",""type"":""address""},{""internalType"":""enum WFRights"",""name"":""right"",""type"":""uint8""}],""name"":""makeRightsValue"",""outputs"":[{""internalType"":""uint256"",""name"":"""",""type"":""uint256""}],""stateMutability"":""pure"",""type"":""function""}]";

        public const string MANAGER_ABI =
        @"[{""inputs"":[],""stateMutability"":""nonpayable"",""type"":""constructor""},{""anonymous"":false,""inputs"":[{""indexed"":true,""internalType"":""uint256"",""name"":""id"",""type"":""uint256""}," +
        @"{""indexed"":false,""internalType"":""address"",""name"":""addr"",""type"":""address""}],""name"":""EventWFAdded"",""type"":""event""},{""anonymous"":false,""inputs"":[{""indexed"":true,""internalType"":""uint256"",""name"":""id"",""type"":""uint256""}," +
        @"{""indexed"":false,""internalType"":""address"",""name"":""addr"",""type"":""address""}],""name"":""EventWFDeleted"",""type"":""event""},{""anonymous"":false,""inputs"":[{""indexed"":true,""internalType"":""bytes32"",""name"":""role"",""type"":""bytes32""}," +
        @"{""indexed"":true,""internalType"":""bytes32"",""name"":""previousAdminRole"",""type"":""bytes32""},{""indexed"":true,""internalType"":""bytes32"",""name"":""newAdminRole"",""type"":""bytes32""}],""name"":""RoleAdminChanged"",""type"":""event""}," +
        @"{""anonymous"":false,""inputs"":[{""indexed"":true,""internalType"":""bytes32"",""name"":""role"",""type"":""bytes32""},{""indexed"":true,""internalType"":""address"",""name"":""account"",""type"":""address""}," +
        @"{""indexed"":true,""internalType"":""address"",""name"":""sender"",""type"":""address""}],""name"":""RoleGranted"",""type"":""event""},{""anonymous"":false,""inputs"":[{""indexed"":true,""internalType"":""bytes32"",""name"":""role"",""type"":""bytes32""}," +
        @"{""indexed"":true,""internalType"":""address"",""name"":""account"",""type"":""address""},{""indexed"":true,""internalType"":""address"",""name"":""sender"",""type"":""address""}],""name"":""RoleRevoked"",""type"":""event""}," +
        @"{""inputs"":[],""name"":""DEFAULT_ADMIN_ROLE"",""outputs"":[{""internalType"":""bytes32"",""name"":"""",""type"":""bytes32""}],""stateMutability"":""view"",""type"":""function""}," +
        @"{""inputs"":[],""name"":""WF_ADMIN_ROLE"",""outputs"":[{""internalType"":""bytes32"",""name"":"""",""type"":""bytes32""}],""stateMutability"":""view"",""type"":""function""}," +
        @"{""inputs"":[{""internalType"":""uint256"",""name"":"""",""type"":""uint256""}],""name"":""closedWFs"",""outputs"":[{""internalType"":""address"",""name"":"""",""type"":""address""}],""stateMutability"":""view"",""type"":""function""}," +
        @"{""inputs"":[{""internalType"":""bytes32"",""name"":""role"",""type"":""bytes32""}],""name"":""getRoleAdmin"",""outputs"":[{""internalType"":""bytes32"",""name"":"""",""type"":""bytes32""}],""stateMutability"":""view"",""type"":""function""}," +
        @"{""inputs"":[{""internalType"":""bytes32"",""name"":""role"",""type"":""bytes32""},{""internalType"":""uint256"",""name"":""index"",""type"":""uint256""}],""name"":""getRoleMember"",""outputs"":[{""internalType"":""address"",""name"":"""",""type"":""address""}],""stateMutability"":""view"",""type"":""function""}," +
        @"{""inputs"":[{""internalType"":""bytes32"",""name"":""role"",""type"":""bytes32""}],""name"":""getRoleMemberCount"",""outputs"":[{""internalType"":""uint256"",""name"":"""",""type"":""uint256""}],""stateMutability"":""view"",""type"":""function""}," +
        @"{""inputs"":[{""internalType"":""bytes32"",""name"":""role"",""type"":""bytes32""},{""internalType"":""address"",""name"":""account"",""type"":""address""}],""name"":""grantRole"",""outputs"":[],""stateMutability"":""nonpayable"",""type"":""function""}," +
        @"{""inputs"":[{""internalType"":""bytes32"",""name"":""role"",""type"":""bytes32""},{""internalType"":""address"",""name"":""account"",""type"":""address""}],""name"":""hasRole"",""outputs"":[{""internalType"":""bool"",""name"":"""",""type"":""bool""}],""stateMutability"":""view"",""type"":""function""}," +
        @"{""inputs"":[],""name"":""nextWF"",""outputs"":[{""internalType"":""uint256"",""name"":"""",""type"":""uint256""}],""stateMutability"":""view"",""type"":""function""},{""inputs"":[{""internalType"":""bytes32"",""name"":""role"",""type"":""bytes32""}," +
        @"{""internalType"":""address"",""name"":""account"",""type"":""address""}],""name"":""renounceRole"",""outputs"":[],""stateMutability"":""nonpayable"",""type"":""function""},{""inputs"":[{""internalType"":""bytes32"",""name"":""role"",""type"":""bytes32""}," +
        @"{""internalType"":""address"",""name"":""account"",""type"":""address""}],""name"":""revokeRole"",""outputs"":[],""stateMutability"":""nonpayable"",""type"":""function""},{""inputs"":[],""name"":""totalWFs"",""outputs"":[{""internalType"":""uint256"",""name"":"""",""type"":""uint256""}],""stateMutability"":""view"",""type"":""function""}," +
        @"{""inputs"":[{""internalType"":""address"",""name"":""eng"",""type"":""address""},{""internalType"":""uint256[]"",""name"":""docs"",""type"":""uint256[]""}],""name"":""addWF"",""outputs"":[],""stateMutability"":""nonpayable"",""type"":""function""}," +
        @"{""inputs"":[{""internalType"":""uint256"",""name"":""id"",""type"":""uint256""}],""name"":""removeWF"",""outputs"":[],""stateMutability"":""nonpayable"",""type"":""function""},{""inputs"":[{""internalType"":""uint256"",""name"":""start"",""type"":""uint256""}," +
        @"{""internalType"":""uint256"",""name"":""toRead"",""type"":""uint256""}],""name"":""readWF"",""outputs"":[{""internalType"":""address[]"",""name"":""addrList"",""type"":""address[]""},{""internalType"":""uint256"",""name"":""next"",""type"":""uint256""}],""stateMutability"":""view"",""type"":""function""}," +
        @"{""inputs"":[],""name"":""firstWF"",""outputs"":[{""internalType"":""uint256"",""name"":"""",""type"":""uint256""}],""stateMutability"":""view"",""type"":""function""},{""inputs"":[],""name"":""lastWF"",""outputs"":[{""internalType"":""uint256"",""name"":"""",""type"":""uint256""}],""stateMutability"":""view"",""type"":""function""}," +
        @"{""inputs"":[],""name"":""totalClosedWFs"",""outputs"":[{""internalType"":""uint256"",""name"":"""",""type"":""uint256""}],""stateMutability"":""view"",""type"":""function""}]";

        public const string WORKFLOW_ABI =
        @"[{""inputs"":[{""internalType"":""address"",""name"":""eng"",""type"":""address""},{""internalType"":""uint256[]"",""name"":""docs"",""type"":""uint256[]""},{""internalType"":""address"",""name"":""addrWFRmv"",""type"":""address""}," +
        @"{""internalType"":""uint256"",""name"":""id"",""type"":""uint256""}],""stateMutability"":""nonpayable"",""type"":""constructor""},{""inputs"":[],""name"":""engine"",""outputs"":[{""internalType"":""contract IStateEngine"",""name"":"""",""type"":""address""}],""stateMutability"":""view"",""type"":""function""}," +
        @"{""inputs"":[{""internalType"":""uint256"",""name"":"""",""type"":""uint256""}],""name"":""latest"",""outputs"":[{""internalType"":""uint256"",""name"":"""",""type"":""uint256""}],""stateMutability"":""view"",""type"":""function""}," +
        @"{""inputs"":[],""name"":""mode"",""outputs"":[{""internalType"":""enum WFMode"",""name"":"""",""type"":""uint8""}],""stateMutability"":""view"",""type"":""function""},{""inputs"":[],""name"":""state"",""outputs"":[{""internalType"":""uint32"",""name"":"""",""type"":""uint32""}],""stateMutability"":""view"",""type"":""function""}," +
        @"{""inputs"":[],""name"":""totalDocTypes"",""outputs"":[{""internalType"":""uint32"",""name"":"""",""type"":""uint32""}],""stateMutability"":""view"",""type"":""function""},{""inputs"":[],""name"":""wfID"",""outputs"":[{""internalType"":""uint256"",""name"":"""",""type"":""uint256""}],""stateMutability"":""view"",""type"":""function""}," +
        @"{""inputs"":[{""internalType"":""uint32"",""name"":""nextState"",""type"":""uint32""},{""internalType"":""uint256[]"",""name"":""ids"",""type"":""uint256[]""},{""internalType"":""uint256[]"",""name"":""content"",""type"":""uint256[]""}],""name"":""doInit"",""outputs"":[],""stateMutability"":""nonpayable"",""type"":""function""}," +
        @"{""inputs"":[{""internalType"":""uint32"",""name"":""nextState"",""type"":""uint32""}],""name"":""doApprove"",""outputs"":[],""stateMutability"":""nonpayable"",""type"":""function""},{""inputs"":[{""internalType"":""uint256[]"",""name"":""idsRmv"",""type"":""uint256[]""}," +
        @"{""internalType"":""uint256[]"",""name"":""idsAdd"",""type"":""uint256[]""},{""internalType"":""uint256[]"",""name"":""contentAdd"",""type"":""uint256[]""}],""name"":""doReview"",""outputs"":[],""stateMutability"":""nonpayable"",""type"":""function""}," +
        @"{""inputs"":[{""internalType"":""uint32"",""name"":""nextState"",""type"":""uint32""}],""name"":""doSignoff"",""outputs"":[],""stateMutability"":""nonpayable"",""type"":""function""},{""inputs"":[{""internalType"":""uint32"",""name"":""nextState"",""type"":""uint32""}],""name"":""doAbort"",""outputs"":[],""stateMutability"":""nonpayable"",""type"":""function""}," +
        @"{""inputs"":[{""internalType"":""uint32"",""name"":""docType"",""type"":""uint32""}],""name"":""getDocProps"",""outputs"":[{""internalType"":""uint32"",""name"":""flags"",""type"":""uint32""},{""internalType"":""int32"",""name"":""loLimit"",""type"":""int32""}," +
        @"{""internalType"":""int32"",""name"":""hiLimit"",""type"":""int32""},{""internalType"":""int32"",""name"":""count"",""type"":""int32""}],""stateMutability"":""view"",""type"":""function""},{""inputs"":[{""internalType"":""uint256"",""name"":""idx"",""type"":""uint256""}],""name"":""getHistory"",""outputs"":[{""internalType"":""address"",""name"":""user"",""type"":""address""}," +
        @"{""internalType"":""enum WFRights"",""name"":""action"",""type"":""uint8""},{""internalType"":""uint32"",""name"":""stateNow"",""type"":""uint32""},{""internalType"":""uint256[]"",""name"":""idsRmv"",""type"":""uint256[]""}," +
        @"{""internalType"":""uint256[]"",""name"":""idsAdd"",""type"":""uint256[]""},{""internalType"":""uint256[]"",""name"":""contentAdd"",""type"":""uint256[]""}],""stateMutability"":""view"",""type"":""function""},{""inputs"":[],""name"":""totalHistory"",""outputs"":[{""internalType"":""uint256"",""name"":"""",""type"":""uint256""}],""stateMutability"":""view"",""type"":""function""}]";

        public const string BUILDER_ADDR = "0x4f610cd142dd40FAA73fCeDd03C40fDfa47502C4";
        public const string MANAGER_ADDR = "0x99bAeF29dE85e3424d5b76937963B454eF8Ec7d2";

        public static BigInteger DefaultGasPrice()
        {
            return 20 * BigInteger.Pow(10, 9);
        }

        public static BigInteger EURRate()
        {
            return 320;
        }

        public static HexBigInteger FinalGasPrice(BigInteger? requested)
        {
            return new HexBigInteger(requested ?? DefaultGasPrice());
        }

        public static BigInteger Estimate(BigInteger gas, EstTyp typ, BigInteger? gasPrice)
        {
            switch (typ)
            {
                case EstTyp.GAS:    return gas;
                case EstTyp.WEI:    return gas * (gasPrice ?? DefaultGasPrice());
            }

            return gas * (gasPrice ?? DefaultGasPrice()) * EURRate();
        }
    }
}
