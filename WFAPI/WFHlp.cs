using System.Numerics;
using Nethereum.Hex.HexTypes;

namespace WFApi
{
    public enum WFErr
    {
        WFFail,                 // Generic when nothing else matches

        WFInvalidSE,            // "Workflow: Invalid State Engine address"
        WFInvalidWFM,           // "Workflow: Invalid WorkflowManager address"
        WFEmptyDocSet,          // "Workflow: Empty Doc Set"
        WFWrongUSN,             // "Workflow: USN mismatch"
        WFNotUninit,            // "Workflow: Only when UNINIT"
        WFCannotCross,          // "Workflow: Unauthorized state crossing"
        WFSkewedInput,          // "Workflow: ids/content array length mismatch"
        WFInvalidDocTyp,        // "Workflow: Invalid doc type"
        WFInvalidDocHash,       // "Workflow: Invalid doc hash"
        WFDocTypLimit,          // "Workflow: Doc type count exceeded limit"
        WFRepeatedDocId,        // "Workflow: Initializing same ID x times"
        WFMissingDoc,           // "Workflow: Required files missing"
        WFNotRunning,           // "Workflow: Only when RUNNING"
        WFEmptyReview,          // "Workflow: No changes submitted"
        WFDocNotFound,          // "Workflow: Doc not found"

        WFBStateNotFound,       // "WFBuilder: Non-existing state"
        WFBUnauth,              // "WFBuilder: Unauthorized"
        WFBFinal,               // "WFBuilder: Workflow is final"
        WFBEdgeToZero,          // "WFBuilder: Edge cannot point to State Zero"
        WFBEdgeBroken,          // "WFBuilder: Edge points to non-existing State"
        WFBNotFinal,            // "WFBuilder: Workflow is not final"
        WFBEdgeNotFound,        // "WFBuilder: Non-existing edge"
        WFBRightNotAllowed,     // "WFBuilder: Right not allowed for this edge"
        WFBRightChange,         // "WFBuilder: Inconsistent rights"
        WFBNoSuchRight,         // "WFBuilder: User/Right not found"
        WFBRightNotFound,       // "WFBuilder: Non-existing Right"

        WFMUnauth,              // "WFManager: Unauthorized"
        WFMUninitWF,            // "WFManager: Uninitialized WF cannot be deleted"
        WFMRunningWF,           // "WFManager: WF still not concluded"
        WFMInvalidPos,          // "WFManager: Invalid reading position."

        AccessCantGrant,        // "AccessControl: sender must be an admin to grant"
        AccessCantRevoke,       // "AccessControl: sender must be an admin to revoke"
        AccessCantRenounce,     // "AccessControl: can only renounce roles for self"
    }

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
        @"{""inputs"":[],""name"":""DEFAULT_ADMIN_ROLE"",""outputs"":[{""internalType"":""bytes32"",""name"":"""",""type"":""bytes32""}],""stateMutability"":""view"",""type"":""function"",""constant"":true}," +
        @"{""inputs"":[],""name"":""WF_SCHEMA_ADMIN_ROLE"",""outputs"":[{""internalType"":""bytes32"",""name"":"""",""type"":""bytes32""}],""stateMutability"":""view"",""type"":""function"",""constant"":true}," +
        @"{""inputs"":[],""name"":""finalized"",""outputs"":[{""internalType"":""bool"",""name"":"""",""type"":""bool""}],""stateMutability"":""view"",""type"":""function"",""constant"":true}," +
        @"{""inputs"":[{""internalType"":""bytes32"",""name"":""role"",""type"":""bytes32""}],""name"":""getRoleAdmin"",""outputs"":[{""internalType"":""bytes32"",""name"":"""",""type"":""bytes32""}],""stateMutability"":""view"",""type"":""function"",""constant"":true}," +
        @"{""inputs"":[{""internalType"":""bytes32"",""name"":""role"",""type"":""bytes32""},{""internalType"":""uint256"",""name"":""index"",""type"":""uint256""}],""name"":""getRoleMember"",""outputs"":[{""internalType"":""address"",""name"":"""",""type"":""address""}],""stateMutability"":""view"",""type"":""function"",""constant"":true}," +
        @"{""inputs"":[{""internalType"":""bytes32"",""name"":""role"",""type"":""bytes32""}],""name"":""getRoleMemberCount"",""outputs"":[{""internalType"":""uint256"",""name"":"""",""type"":""uint256""}],""stateMutability"":""view"",""type"":""function"",""constant"":true}," +
        @"{""inputs"":[{""internalType"":""bytes32"",""name"":""role"",""type"":""bytes32""},{""internalType"":""address"",""name"":""account"",""type"":""address""}],""name"":""grantRole"",""outputs"":[],""stateMutability"":""nonpayable"",""type"":""function""}," +
        @"{""inputs"":[{""internalType"":""bytes32"",""name"":""role"",""type"":""bytes32""},{""internalType"":""address"",""name"":""account"",""type"":""address""}],""name"":""hasRole"",""outputs"":[{""internalType"":""bool"",""name"":"""",""type"":""bool""}],""stateMutability"":""view"",""type"":""function"",""constant"":true}," +
        @"{""inputs"":[{""internalType"":""bytes32"",""name"":""role"",""type"":""bytes32""},{""internalType"":""address"",""name"":""account"",""type"":""address""}],""name"":""renounceRole"",""outputs"":[],""stateMutability"":""nonpayable"",""type"":""function""}," +
        @"{""inputs"":[{""internalType"":""bytes32"",""name"":""role"",""type"":""bytes32""},{""internalType"":""address"",""name"":""account"",""type"":""address""}],""name"":""revokeRole"",""outputs"":[],""stateMutability"":""nonpayable"",""type"":""function""}," +
        @"{""inputs"":[{""internalType"":""uint256"",""name"":"""",""type"":""uint256""},{""internalType"":""uint256"",""name"":"""",""type"":""uint256""}],""name"":""states"",""outputs"":[{""internalType"":""uint32"",""name"":"""",""type"":""uint32""}],""stateMutability"":""view"",""type"":""function"",""constant"":true}," +
        @"{""inputs"":[],""name"":""usn"",""outputs"":[{""internalType"":""uint256"",""name"":"""",""type"":""uint256""}],""stateMutability"":""view"",""type"":""function"",""constant"":true}," +
        @"{""inputs"":[{""internalType"":""uint32[]"",""name"":""edges"",""type"":""uint32[]""}],""name"":""addState"",""outputs"":[],""stateMutability"":""nonpayable"",""type"":""function""}," +
        @"{""inputs"":[],""name"":""doFinalize"",""outputs"":[],""stateMutability"":""nonpayable"",""type"":""function""},{""inputs"":[{""internalType"":""uint32"",""name"":""stateid"",""type"":""uint32""}," +
        @"{""internalType"":""uint32"",""name"":""edgeid"",""type"":""uint32""},{""internalType"":""address"",""name"":""user"",""type"":""address""},{""internalType"":""enum WFRights"",""name"":""right"",""type"":""uint8""}],""name"":""addRight"",""outputs"":[],""stateMutability"":""nonpayable"",""type"":""function""}," +
        @"{""inputs"":[{""internalType"":""uint32"",""name"":""stateid"",""type"":""uint32""},{""internalType"":""uint32"",""name"":""edgeid"",""type"":""uint32""},{""internalType"":""address"",""name"":""user"",""type"":""address""}," +
        @"{""internalType"":""enum WFRights"",""name"":""right"",""type"":""uint8""}],""name"":""removeRight"",""outputs"":[],""stateMutability"":""nonpayable"",""type"":""function""}," +
        @"{""inputs"":[],""name"":""getTotalStates"",""outputs"":[{""internalType"":""uint32"",""name"":"""",""type"":""uint32""}],""stateMutability"":""view"",""type"":""function"",""constant"":true}," +
        @"{""inputs"":[{""internalType"":""uint32"",""name"":""stateid"",""type"":""uint32""}],""name"":""getTotalEdges"",""outputs"":[{""internalType"":""uint32"",""name"":"""",""type"":""uint32""}],""stateMutability"":""view"",""type"":""function"",""constant"":true}," +
        @"{""inputs"":[{""internalType"":""uint32"",""name"":""stateid"",""type"":""uint32""},{""internalType"":""uint32"",""name"":""edgeid"",""type"":""uint32""}],""name"":""getTotalRights"",""outputs"":[{""internalType"":""uint32"",""name"":"""",""type"":""uint32""}],""stateMutability"":""view"",""type"":""function"",""constant"":true}," +
        @"{""inputs"":[{""internalType"":""uint32"",""name"":""state1"",""type"":""uint32""},{""internalType"":""uint32"",""name"":""state2"",""type"":""uint32""},{""internalType"":""address"",""name"":""user"",""type"":""address""}," +
        @"{""internalType"":""enum WFRights"",""name"":""right"",""type"":""uint8""}],""name"":""hasRight"",""outputs"":[{""internalType"":""bool"",""name"":"""",""type"":""bool""}],""stateMutability"":""view"",""type"":""function"",""constant"":true}," +
        @"{""inputs"":[{""internalType"":""uint32"",""name"":""stateid"",""type"":""uint32""},{""internalType"":""uint32"",""name"":""edgeid"",""type"":""uint32""}," +
        @"{""internalType"":""uint32"",""name"":""rightid"",""type"":""uint32""}],""name"":""getRight"",""outputs"":[{""internalType"":""address"",""name"":""user"",""type"":""address""},{""internalType"":""enum WFRights"",""name"":""right"",""type"":""uint8""}],""stateMutability"":""view"",""type"":""function"",""constant"":true}," +
        @"{""inputs"":[{""internalType"":""uint32"",""name"":""stateid"",""type"":""uint32""},{""internalType"":""uint32"",""name"":""edgeid"",""type"":""uint32""}],""name"":""makeRightsKey"",""outputs"":[{""internalType"":""uint256"",""name"":"""",""type"":""uint256""}],""stateMutability"":""pure"",""type"":""function"",""constant"":true}," +
        @"{""inputs"":[{""internalType"":""address"",""name"":""user"",""type"":""address""},{""internalType"":""enum WFRights"",""name"":""right"",""type"":""uint8""}],""name"":""makeRightsValue"",""outputs"":[{""internalType"":""uint256"",""name"":"""",""type"":""uint256""}],""stateMutability"":""pure"",""type"":""function"",""constant"":true}]";

        public const string MANAGER_ABI =
        @"[{""inputs"":[],""stateMutability"":""nonpayable"",""type"":""constructor""},{""anonymous"":false,""inputs"":[{""indexed"":true,""internalType"":""uint256"",""name"":""id"",""type"":""uint256""}," +
        @"{""indexed"":false,""internalType"":""address"",""name"":""addr"",""type"":""address""}],""name"":""EventWFAdded"",""type"":""event""},{""anonymous"":false,""inputs"":[{""indexed"":true,""internalType"":""uint256"",""name"":""id"",""type"":""uint256""}," +
        @"{""indexed"":false,""internalType"":""address"",""name"":""addr"",""type"":""address""}],""name"":""EventWFDeleted"",""type"":""event""},{""anonymous"":false,""inputs"":[{""indexed"":true,""internalType"":""bytes32"",""name"":""role"",""type"":""bytes32""}," +
        @"{""indexed"":true,""internalType"":""bytes32"",""name"":""previousAdminRole"",""type"":""bytes32""},{""indexed"":true,""internalType"":""bytes32"",""name"":""newAdminRole"",""type"":""bytes32""}],""name"":""RoleAdminChanged"",""type"":""event""}," +
        @"{""anonymous"":false,""inputs"":[{""indexed"":true,""internalType"":""bytes32"",""name"":""role"",""type"":""bytes32""},{""indexed"":true,""internalType"":""address"",""name"":""account"",""type"":""address""}," +
        @"{""indexed"":true,""internalType"":""address"",""name"":""sender"",""type"":""address""}],""name"":""RoleGranted"",""type"":""event""},{""anonymous"":false,""inputs"":[{""indexed"":true,""internalType"":""bytes32"",""name"":""role"",""type"":""bytes32""}," +
        @"{""indexed"":true,""internalType"":""address"",""name"":""account"",""type"":""address""},{""indexed"":true,""internalType"":""address"",""name"":""sender"",""type"":""address""}],""name"":""RoleRevoked"",""type"":""event""}," +
        @"{""inputs"":[],""name"":""DEFAULT_ADMIN_ROLE"",""outputs"":[{""internalType"":""bytes32"",""name"":"""",""type"":""bytes32""}],""stateMutability"":""view"",""type"":""function"",""constant"":true}," +
        @"{""inputs"":[],""name"":""WF_ADMIN_ROLE"",""outputs"":[{""internalType"":""bytes32"",""name"":"""",""type"":""bytes32""}],""stateMutability"":""view"",""type"":""function"",""constant"":true},{""inputs"":[{""internalType"":""uint256"",""name"":"""",""type"":""uint256""}],""name"":""closedWFs"",""outputs"":[{""internalType"":""address"",""name"":"""",""type"":""address""}],""stateMutability"":""view"",""type"":""function"",""constant"":true}," +
        @"{""inputs"":[{""internalType"":""bytes32"",""name"":""role"",""type"":""bytes32""}],""name"":""getRoleAdmin"",""outputs"":[{""internalType"":""bytes32"",""name"":"""",""type"":""bytes32""}],""stateMutability"":""view"",""type"":""function"",""constant"":true}," +
        @"{""inputs"":[{""internalType"":""bytes32"",""name"":""role"",""type"":""bytes32""},{""internalType"":""uint256"",""name"":""index"",""type"":""uint256""}],""name"":""getRoleMember"",""outputs"":[{""internalType"":""address"",""name"":"""",""type"":""address""}],""stateMutability"":""view"",""type"":""function"",""constant"":true}," +
        @"{""inputs"":[{""internalType"":""bytes32"",""name"":""role"",""type"":""bytes32""}],""name"":""getRoleMemberCount"",""outputs"":[{""internalType"":""uint256"",""name"":"""",""type"":""uint256""}],""stateMutability"":""view"",""type"":""function"",""constant"":true},{""inputs"":[{""internalType"":""bytes32"",""name"":""role"",""type"":""bytes32""}," +
        @"{""internalType"":""address"",""name"":""account"",""type"":""address""}],""name"":""grantRole"",""outputs"":[],""stateMutability"":""nonpayable"",""type"":""function""},{""inputs"":[{""internalType"":""bytes32"",""name"":""role"",""type"":""bytes32""}," +
        @"{""internalType"":""address"",""name"":""account"",""type"":""address""}],""name"":""hasRole"",""outputs"":[{""internalType"":""bool"",""name"":"""",""type"":""bool""}],""stateMutability"":""view"",""type"":""function"",""constant"":true}," +
        @"{""inputs"":[],""name"":""nextWF"",""outputs"":[{""internalType"":""uint256"",""name"":"""",""type"":""uint256""}],""stateMutability"":""view"",""type"":""function"",""constant"":true},{""inputs"":[{""internalType"":""bytes32"",""name"":""role"",""type"":""bytes32""}," +
        @"{""internalType"":""address"",""name"":""account"",""type"":""address""}],""name"":""renounceRole"",""outputs"":[],""stateMutability"":""nonpayable"",""type"":""function""},{""inputs"":[{""internalType"":""bytes32"",""name"":""role"",""type"":""bytes32""}," +
        @"{""internalType"":""address"",""name"":""account"",""type"":""address""}],""name"":""revokeRole"",""outputs"":[],""stateMutability"":""nonpayable"",""type"":""function""},{""inputs"":[],""name"":""totalWFs"",""outputs"":[{""internalType"":""uint256"",""name"":"""",""type"":""uint256""}],""stateMutability"":""view"",""type"":""function"",""constant"":true}," +
        @"{""inputs"":[{""internalType"":""address"",""name"":""eng"",""type"":""address""},{""internalType"":""uint256[]"",""name"":""docs"",""type"":""uint256[]""}],""name"":""addWF"",""outputs"":[],""stateMutability"":""nonpayable"",""type"":""function""}," +
        @"{""inputs"":[{""internalType"":""uint256"",""name"":""id"",""type"":""uint256""}],""name"":""removeWF"",""outputs"":[],""stateMutability"":""nonpayable"",""type"":""function""},{""inputs"":[{""internalType"":""uint256"",""name"":""start"",""type"":""uint256""}," +
        @"{""internalType"":""uint256"",""name"":""toRead"",""type"":""uint256""}],""name"":""readWF"",""outputs"":[{""internalType"":""address[]"",""name"":""addrList"",""type"":""address[]""},{""internalType"":""uint256"",""name"":""next"",""type"":""uint256""}],""stateMutability"":""view"",""type"":""function"",""constant"":true}," +
        @"{""inputs"":[],""name"":""firstWF"",""outputs"":[{""internalType"":""uint256"",""name"":"""",""type"":""uint256""}],""stateMutability"":""view"",""type"":""function"",""constant"":true}," +
        @"{""inputs"":[],""name"":""lastWF"",""outputs"":[{""internalType"":""uint256"",""name"":"""",""type"":""uint256""}],""stateMutability"":""view"",""type"":""function"",""constant"":true},{""inputs"":[],""name"":""totalClosedWFs"",""outputs"":[{""internalType"":""uint256"",""name"":"""",""type"":""uint256""}],""stateMutability"":""view"",""type"":""function"",""constant"":true}]";

        public const string WORKFLOW_ABI =
        @"[{""inputs"":[{""internalType"":""address"",""name"":""eng"",""type"":""address""},{""internalType"":""uint256[]"",""name"":""docs"",""type"":""uint256[]""},{""internalType"":""address"",""name"":""addrWFRmv"",""type"":""address""}," +
        @"{""internalType"":""uint256"",""name"":""id"",""type"":""uint256""}],""stateMutability"":""nonpayable"",""type"":""constructor""},{""inputs"":[],""name"":""engine"",""outputs"":[{""internalType"":""contract IStateEngine"",""name"":"""",""type"":""address""}],""stateMutability"":""view"",""type"":""function""}," +
        @"{""inputs"":[{""internalType"":""uint256"",""name"":"""",""type"":""uint256""}],""name"":""latest"",""outputs"":[{""internalType"":""uint256"",""name"":"""",""type"":""uint256""}],""stateMutability"":""view"",""type"":""function""}," +
        @"{""inputs"":[],""name"":""mode"",""outputs"":[{""internalType"":""enum WFMode"",""name"":"""",""type"":""uint8""}],""stateMutability"":""view"",""type"":""function""}," +
        @"{""inputs"":[],""name"":""state"",""outputs"":[{""internalType"":""uint32"",""name"":"""",""type"":""uint32""}],""stateMutability"":""view"",""type"":""function""}," +
        @"{""inputs"":[],""name"":""totalDocTypes"",""outputs"":[{""internalType"":""uint32"",""name"":"""",""type"":""uint32""}],""stateMutability"":""view"",""type"":""function""}," +
        @"{""inputs"":[],""name"":""wfID"",""outputs"":[{""internalType"":""uint256"",""name"":"""",""type"":""uint256""}],""stateMutability"":""view"",""type"":""function""}," +
        @"{""inputs"":[{""internalType"":""uint256"",""name"":""usn"",""type"":""uint256""},{""internalType"":""uint32"",""name"":""nextState"",""type"":""uint32""}," +
        @"{""internalType"":""uint256[]"",""name"":""ids"",""type"":""uint256[]""},{""internalType"":""uint256[]"",""name"":""content"",""type"":""uint256[]""}],""name"":""doInit"",""outputs"":[],""stateMutability"":""nonpayable"",""type"":""function""}," +
        @"{""inputs"":[{""internalType"":""uint256"",""name"":""usn"",""type"":""uint256""},{""internalType"":""uint32"",""name"":""nextState"",""type"":""uint32""}],""name"":""doApprove"",""outputs"":[],""stateMutability"":""nonpayable"",""type"":""function""}," +
        @"{""inputs"":[{""internalType"":""uint256"",""name"":""usn"",""type"":""uint256""},{""internalType"":""uint256[]"",""name"":""idsRmv"",""type"":""uint256[]""},{""internalType"":""uint256[]"",""name"":""idsAdd"",""type"":""uint256[]""}," +
        @"{""internalType"":""uint256[]"",""name"":""contentAdd"",""type"":""uint256[]""}],""name"":""doReview"",""outputs"":[],""stateMutability"":""nonpayable"",""type"":""function""}," +
        @"{""inputs"":[{""internalType"":""uint256"",""name"":""usn"",""type"":""uint256""},{""internalType"":""uint32"",""name"":""nextState"",""type"":""uint32""}],""name"":""doSignoff"",""outputs"":[],""stateMutability"":""nonpayable"",""type"":""function""}," +
        @"{""inputs"":[{""internalType"":""uint256"",""name"":""usn"",""type"":""uint256""},{""internalType"":""uint32"",""name"":""nextState"",""type"":""uint32""}],""name"":""doAbort"",""outputs"":[],""stateMutability"":""nonpayable"",""type"":""function""}," +
        @"{""inputs"":[{""internalType"":""uint32"",""name"":""docType"",""type"":""uint32""}],""name"":""getDocProps"",""outputs"":[{""internalType"":""uint32"",""name"":""flags"",""type"":""uint32""},{""internalType"":""int32"",""name"":""loLimit"",""type"":""int32""}," +
        @"{""internalType"":""int32"",""name"":""hiLimit"",""type"":""int32""},{""internalType"":""int32"",""name"":""count"",""type"":""int32""}],""stateMutability"":""view"",""type"":""function""}," +
        @"{""inputs"":[{""internalType"":""uint256"",""name"":""idx"",""type"":""uint256""}],""name"":""getHistory"",""outputs"":[{""internalType"":""address"",""name"":""user"",""type"":""address""}," +
        @"{""internalType"":""enum WFRights"",""name"":""action"",""type"":""uint8""},{""internalType"":""uint32"",""name"":""stateNow"",""type"":""uint32""},{""internalType"":""uint256[]"",""name"":""idsRmv"",""type"":""uint256[]""}," +
        @"{""internalType"":""uint256[]"",""name"":""idsAdd"",""type"":""uint256[]""},{""internalType"":""uint256[]"",""name"":""contentAdd"",""type"":""uint256[]""}],""stateMutability"":""view"",""type"":""function""}," +
        @"{""inputs"":[],""name"":""totalHistory"",""outputs"":[{""internalType"":""uint256"",""name"":"""",""type"":""uint256""}],""stateMutability"":""view"",""type"":""function""}]";


        public const string BUILDER_ADDR_ROPSTEN = "0x1D30975bA198B0FF8dC95e522472Bd5047BAbA39";
        public const string MANAGER_ADDR_ROPSTEN = "0x522CDa6A8eC47a179D2C27fC68DA836A7C54d699";

        public static string BUILDER_ADDR { get; set; } = BUILDER_ADDR_ROPSTEN;
        public static string MANAGER_ADDR { get; set; } = MANAGER_ADDR_ROPSTEN;

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

        public static WFErr GetErr(string sMsg)
        {
            if (string.IsNullOrWhiteSpace(sMsg))
                return WFErr.WFFail;

            if (sMsg.IndexOf("Workflow: Invalid State Engine address") != -1)                   return WFErr.WFInvalidSE;
            else if (sMsg.IndexOf("Workflow: Invalid WorkflowManager address") != -1)           return WFErr.WFInvalidWFM;
            else if (sMsg.IndexOf("Workflow: Empty Doc Set") != -1)                             return WFErr.WFEmptyDocSet;
            else if (sMsg.IndexOf("Workflow: USN mismatch") != -1)                              return WFErr.WFWrongUSN;
            else if (sMsg.IndexOf("Workflow: Only when UNINIT") != -1)                          return WFErr.WFNotUninit;
            else if (sMsg.IndexOf("Workflow: Unauthorized state crossing") != -1)               return WFErr.WFCannotCross;
            else if (sMsg.IndexOf("Workflow: ids/content array length mismatch") != -1)         return WFErr.WFSkewedInput;
            else if (sMsg.IndexOf("Workflow: Invalid doc type") != -1)                          return WFErr.WFInvalidDocTyp;
            else if (sMsg.IndexOf("Workflow: Invalid doc hash") != -1)                          return WFErr.WFInvalidDocHash;
            else if (sMsg.IndexOf("Workflow: Doc type count exceeded limit") != -1)             return WFErr.WFDocTypLimit;
            else if (sMsg.IndexOf("Workflow: Initializing same ID x times") != -1)              return WFErr.WFRepeatedDocId;
            else if (sMsg.IndexOf("Workflow: Required files missing") != -1)                    return WFErr.WFMissingDoc;
            else if (sMsg.IndexOf("Workflow: Only when RUNNING") != -1)                         return WFErr.WFNotRunning;
            else if (sMsg.IndexOf("Workflow: No changes submitted") != -1)                      return WFErr.WFEmptyReview;
            else if (sMsg.IndexOf("Workflow: Doc not found") != -1)                             return WFErr.WFDocNotFound;

            else if (sMsg.IndexOf("WFBuilder: Non-existing state") != -1)                       return WFErr.WFBStateNotFound;
            else if (sMsg.IndexOf("WFBuilder: Unauthorized") != -1)                             return WFErr.WFBUnauth;
            else if (sMsg.IndexOf("WFBuilder: Workflow is final") != -1)                        return WFErr.WFBFinal;
            else if (sMsg.IndexOf("WFBuilder: Edge cannot point to State Zero") != -1)          return WFErr.WFBEdgeToZero;
            else if (sMsg.IndexOf("WFBuilder: Edge points to non-existing State") != -1)        return WFErr.WFBEdgeBroken;
            else if (sMsg.IndexOf("WFBuilder: Workflow is not final") != -1)                    return WFErr.WFBNotFinal;
            else if (sMsg.IndexOf("WFBuilder: Non-existing edge") != -1)                        return WFErr.WFBEdgeNotFound;
            else if (sMsg.IndexOf("WFBuilder: Right not allowed for this edge") != -1)          return WFErr.WFBRightNotAllowed;
            else if (sMsg.IndexOf("WFBuilder: Inconsistent rights") != -1)                      return WFErr.WFBRightChange;
            else if (sMsg.IndexOf("WFBuilder: User/Right not found") != -1)                     return WFErr.WFBNoSuchRight;
            else if (sMsg.IndexOf("WFBuilder: Non-existing Right") != -1)                       return WFErr.WFBRightNotFound;

            else if (sMsg.IndexOf("WFManager: Unauthorized") != -1)                             return WFErr.WFMUnauth;
            else if (sMsg.IndexOf("WFManager: Uninitialized WF cannot be deleted") != -1)       return WFErr.WFMUninitWF;
            else if (sMsg.IndexOf("WFManager: WF still not concluded") != -1)                   return WFErr.WFMRunningWF;
            else if (sMsg.IndexOf("WFManager: Invalid reading position.") != -1)                return WFErr.WFMInvalidPos;

            else if (sMsg.IndexOf("AccessControl: sender must be an admin to grant") != -1)     return WFErr.AccessCantGrant;
            else if (sMsg.IndexOf("AccessControl: sender must be an admin to revoke") != -1)    return WFErr.AccessCantRevoke;
            else if (sMsg.IndexOf("AccessControl: can only renounce roles for self") != -1)     return WFErr.AccessCantRenounce;

            return WFErr.WFFail;
        }
    }
}
