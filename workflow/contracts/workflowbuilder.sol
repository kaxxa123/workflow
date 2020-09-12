pragma solidity ^0.6.0;

import "./Interfaces.sol";
import "@openzeppelin/contracts/access/AccessControl.sol";

/// @title A contract to build & manage a state engine
/// @author Alexander Zammit
contract WorkflowBuilder is IStateEngine, AccessControl {

    bytes32 public constant WF_SCHEMA_ADMIN_ROLE = keccak256("WF_SCHEMA_ADMIN_ROLE");

    //All States/Edges are stored as an array of arrays
    //Such that: states[InitialState][EdgeIdx] = EndState
    uint32[][] public states;

    //For each edge we store an array of rights
    //Key = <initial state><edge index>
    //Value = Array of Rights - [<right><address>]
    mapping(uint256 => uint256[]) private rights;

    //Universal Sequence Number allowing us to quickly see if the WF changed
    uint256 public  usn;

    //Is WF structure final?
    //true  - all states/edges are defined
    //false - more states/edges can be added
    bool    public  finalized;


    /// @dev Initializes WorkflowBuilder
    constructor() public {
        _setupRole(DEFAULT_ADMIN_ROLE, msg.sender);
    }

    /// @dev Append a new state.
    /// @param edges an array of edges identifying the set of connected state
    function addState(uint32[] calldata edges) external {
        require(hasRole(WF_SCHEMA_ADMIN_ROLE, msg.sender), "WFBuilder: Unauthorized");
        require(!finalized, "WFBuilder: Workflow is final");
        states.push();
        states[states.length-1] = edges;
        ++usn;
    }

    /// @dev Mark state engine structure as final
    function doFinalize() external {
        require(hasRole(WF_SCHEMA_ADMIN_ROLE, msg.sender), "WFBuilder: Unauthorized");
        require(!finalized, "WFBuilder: Workflow is final");
        //Basic state engine validation. 
        //Make sure all edges refer to a valid End State
        uint uTotStates = states.length;
        for(uint uStates = 0; uStates < uTotStates; ++uStates) {
            uint32[] storage edges = states[uStates];
            uint uTotEdges = edges.length;

            for(uint uEdges = 0; uEdges < uTotEdges; ++uEdges) {
                uint endState = edges[uEdges];
                require(endState != 0,"WFBuilder: Edge cannot point to State Zero");
                require(endState < uTotStates,"WFBuilder: Edge points to non-existing State");
            }
        }

        finalized = true;
    }

    /// @dev Add Right to an Edge
    /// @param stateid state index. Defined on calling addState. First state gets index 0, next 1 etc,
    /// @param edgeid edge index. This matches the array of edges fed to addState
    /// @param user address of user to whom the right is being assigned
    /// @param right the right being assigned
    function addRight(uint32 stateid, uint32 edgeid, address user, WFRights right) external {
        require(finalized, "WFBuilder: Workflow is not final");
        require(hasRole(WF_SCHEMA_ADMIN_ROLE, msg.sender), "WFBuilder: Unauthorized");
        require(stateid < states.length, "WFBuilder: Non-existing state");
        require(edgeid < states[stateid].length, "WFBuilder: Non-existing edge");
        require(isValidRight(stateid,states[stateid][edgeid],right), "WFBuilder: Right not allowed for this edge");

        uint256 rightKey = makeRightsKey(stateid,edgeid);
        uint256 rigthVal = makeRightsValue(user, right);

        uint256[] storage edgeRights = rights[rightKey];

        if (edgeRights.length > 0) {
            WFRights right0 = WFRights(edgeRights[0] >> 160);
            require(right == right0, "WFBuilder: Inconsistent rights");
        }

        edgeRights.push(rigthVal);
        ++usn;
    }

    /// @dev Revoke Right from an Edge
    /// @param stateid state index. Defined on calling addState. First state gets index 0, next 1 etc,
    /// @param edgeid edge index. This matches the array of edges fed to addState
    /// @param user address of user whose right is being revoked
    /// @param right the right being revoked
    function removeRight(uint32 stateid, uint32 edgeid, address user, WFRights right) external {
        require(finalized, "WFBuilder: Workflow is not final");
        require(hasRole(WF_SCHEMA_ADMIN_ROLE, msg.sender), "WFBuilder: Unauthorized");
        require(stateid < states.length, "WFBuilder: Non-existing state");
        require(edgeid < states[stateid].length, "WFBuilder: Non-existing edge");

        uint256 rightKey = makeRightsKey(stateid,edgeid);
        uint256 rigthVal = makeRightsValue(user, right);

        uint256[] storage edgeRights = rights[rightKey];

        //Lookup & Delete the right
        //Deletion is done by moving around array items
        //This is ok as long as we are dealing with short lists
        for (uint uCnt = 0; uCnt < edgeRights.length; ++uCnt) {
            if (edgeRights[uCnt] == rigthVal) {

                //Delete by overwriting value with the last array item value
                if (uCnt != edgeRights.length-1)
                    edgeRights[uCnt] = edgeRights[edgeRights.length-1];

                edgeRights.pop();
                ++usn;
                return;
            }
        }

        revert("WFBuilder: User/Right not found");        
    }

    /// @dev Get total defined states
    /// @return total states
    function getTotalStates() external view returns (uint32) {
        return uint32(states.length);
    }

    /// @dev Get total edges coming out from a given state
    /// @param stateid state index. Defined on calling addState. First state gets index 0, next 1 etc,
    /// @return total edges for given state
    function getTotalEdges(uint32 stateid) external view returns (uint32) {
        require(stateid < states.length, "WFBuilder: Non-existing state");
        return uint32(states[stateid].length);
    }

    /// @dev Get total rights assigned to a given edge
    /// @param stateid state index. Defined on calling addState. First state gets index 0, next 1 etc,
    /// @param edgeid edge index. This matches the array of edges fed to addState
    /// @return total rights for given edge
    function getTotalRights(uint32 stateid, uint32 edgeid) external view returns (uint32) {
        require(stateid < states.length, "WFBuilder: Non-existing state");
        require(edgeid < states[stateid].length, "WFBuilder: Non-existing edge");

        uint256 rightKey = makeRightsKey(stateid,edgeid);
        return uint32(rights[rightKey].length);
    }

    /// @dev Query if edge includes specified right
    /// @param state1 state index. Defined on calling addState. First state gets index 0, next 1 etc,
    /// @param state2 index of state connected to state1
    /// @param user address of user whose right is being queried
    /// @param right right being queried
    /// @return true if right is present, false otherwise
    function hasRight(uint32 state1, uint32 state2, address user, WFRights right) override external view returns (bool) {
        require(state1 < states.length, "WFBuilder: Non-existing state");
        require(state2 < states.length, "WFBuilder: Non-existing state");

        uint rigthVal = makeRightsValue(user, right);
        for (uint32 uECnt = 0; uECnt < states[state1].length; ++uECnt) {

            if (states[state1][uECnt] != state2)
                continue;

            uint rightKey = makeRightsKey(state1,uECnt);
            for (uint32 uRCnt = 0; uRCnt < rights[rightKey].length; ++uRCnt) {
                if (rights[rightKey][uRCnt] == rigthVal)
                    return true;
            }
        }

        return false;
    }

    /// @dev Query right settings for a given right index under a given edge
    /// @param stateid state index. Defined on calling addState. First state gets index 0, next 1 etc,
    /// @param edgeid edge index. This matches the array of edges fed to addState
    /// @param rightid index of right being queried. This index is determined by the storage order.
    /// It is possible for rights to change order as new rights are added/removed.
    /// To ensure rights didn't change order verify the usn value before/after enumerating rights.
    /// @return user address to whom right applies
    /// @return right user right
    function getRight(uint32 stateid, uint32 edgeid, uint32 rightid) external view returns(address user, WFRights right) {
        require(stateid < states.length, "WFBuilder: Non-existing state");
        require(edgeid < states[stateid].length, "WFBuilder: Non-existing edge");

        uint rightKey = makeRightsKey(stateid,edgeid);

        require(rightid < rights[rightKey].length, "WFBuilder: Non-existing Right");

        user  = address(rights[rightKey][rightid]);
        right = WFRights(rights[rightKey][rightid] >> 160);
    }

    /// @dev Construct key from a given state and edge index to use with the rights mapping
    /// @param stateid state index. Defined on calling addState. First state gets index 0, next 1 etc,
    /// @param edgeid edge index. This matches the array of edges fed to addState
    /// @return rights mapping key
    function makeRightsKey(uint32 stateid, uint32 edgeid) public pure returns(uint256) {
       return (uint256(stateid)<<128) | uint256(edgeid);
    }

    /// @dev Construct value from a given address and right to use with the rights mapping
    /// @param user address of user
    /// @param right right value
    /// @return value encoding together the address and right
    function makeRightsValue(address user, WFRights right) public pure returns(uint256) {
       return (uint256(right)<<160) | uint256(user);
    }

    /// @dev Validate if right is allowed for a given edge
    /// @param state1 state index. Defined on calling addState. First state gets index 0, next 1 etc,
    /// @param state2 index of state connected to state1
    /// @param right right being verified
    /// @return true if right may be assigned to edge, false otherwise 
    function isValidRight(uint32 state1, uint32 state2, WFRights right) private view returns(bool) {
        //INIT is only allowed for S0
        if (right == WFRights.INIT) {
            return (state1 == 0) && (state1 != state2);
        }
        //APPROVE cannot connect from S0 and 
        // MUST cause a transition between states
        // MUST NOT End in a state that has no edges
        else if (right == WFRights.APPROVE) {
            return (state1 != 0) && (state1 != state2) && (states[state2].length > 0);
        }
        //REVIEW cannot connect from S0 and 
        // MUST NOT cause a transition between states
        else if (right == WFRights.REVIEW) {
            return (state1 != 0) && (state1 == state2);
        }
        //SIGNOFF, ABORT cannot connect from S0 and 
        // MUST cause a transition between states
        // MUST end in a state that has no edges
        else if ((right == WFRights.SIGNOFF) ||
                 (right == WFRights.ABORT)) {
            return (state1 != 0) && (state1 != state2) && (states[state2].length == 0);
        }

        return false;
    }
}
