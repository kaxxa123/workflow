pragma solidity ^0.6.0;

import "./Interfaces.sol";

enum WFRights {
    INIT,       // Start Workflow by submitting 1st document set version
    APPROVE,    // Approve the current document set version
    REVIEW,     // Submit an updated document set
    SIGNOFF,    // Conclude workflow successfully
    ABORT       // Abort workflow
}

contract WorkflowBuilder {

    uint32 constant STATE_END_OK    = 0x80000000;
    uint32 constant STATE_END_ABORT = 0x80000001;
    uint32 constant STATE_INVALID   = 0xFFFFFFFF;

    uint32[][] public states;
    mapping(uint256 => uint256[]) public rights;

    uint256 public  usn;
    bool    public  finalized;
    address private owner;

    constructor() public {
        owner = msg.sender;
    }

    function addState(uint32[] memory edges) public {
        require(owner == msg.sender, "Unauthorized");
        require(!finalized, "Workflow is final");
        states.push();
        states[states.length-1] = edges;
        ++usn;
    }

    function doFinalize() external {
        require(owner == msg.sender, "Unauthorized");
        finalized = true;
    }

    function addRight(uint32 stateid, uint32 edgeid, address user, WFRights right) external {
        require(owner == msg.sender, "Unauthorized");
        require(stateid < states.length, "Non-existing state");
        require(edgeid < states[stateid].length, "Non-existing edge");

        uint256 rightKey = makeRightsKey(stateid,edgeid);
        uint256 rigthVal = makeRightsValue(user, right);

        uint256[] storage edgeRights = rights[rightKey];
        edgeRights.push(rigthVal);
        ++usn;
    }

    function removeRight(uint32 stateid, uint32 edgeid, address user, WFRights right) external {
        require(owner == msg.sender, "Unauthorized");
        require(stateid < states.length, "Non-existing state");
        require(edgeid < states[stateid].length, "Non-existing edge");

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

        revert("User/Right not found");        
    }

    function getTotalStates() external view returns (uint32) {
        return uint32(states.length);
    }

    function getTotalEdges(uint32 stateid) external view returns (uint32) {
        require(stateid < states.length, "Non-existing state");
        return uint32(states[stateid].length);
    }

    function getTotalRights(uint32 stateid, uint32 edgeid) external view returns (uint32) {
        require(stateid < states.length, "Non-existing state");
        require(edgeid < states[stateid].length, "Non-existing edge");

        uint256 rightKey = makeRightsKey(stateid,edgeid);
        return uint32(rights[rightKey].length);
    }

    function hasRight(uint32 stateid, address user, WFRights right) external view returns (uint32) {
        require(stateid < states.length, "Non-existing state");

        uint rigthVal = makeRightsValue(user, right);

        for (uint32 uECnt = 0; uECnt < states[stateid].length; ++uECnt) {

            uint rightKey = makeRightsKey(stateid,uECnt);

            for (uint32 uRCnt = 0; uRCnt < rights[rightKey].length; ++uRCnt) {
                if (rights[rightKey][uRCnt] == rigthVal)
                    return states[stateid][uECnt];
            }
        }

        return STATE_INVALID;
    }

    function getRight(uint32 stateid, uint32 edgeid, uint32 rightid) public view returns(address user, WFRights right) {
        require(stateid < states.length, "Non-existing state");
        require(edgeid < states[stateid].length, "Non-existing edge");

        uint rightKey = makeRightsKey(stateid,edgeid);

        require(rightid < rights[rightKey].length, "Non-existing Right");

        user  = address(rights[rightKey][rightid]);
        right = WFRights(rights[rightKey][rightid] >> 160);
    }

    function makeRightsKey(uint32 stateid, uint32 edgeid) public pure returns(uint256) {
       return (uint256(stateid)<<128) | uint256(edgeid);
    }

    function makeRightsValue(address user, WFRights right) public pure returns(uint256) {
       return (uint256(right)<<160) | uint256(user);
    }
}
