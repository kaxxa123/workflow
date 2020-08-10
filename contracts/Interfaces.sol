pragma solidity ^0.6.0;

enum WFMode {
    UNINIT,
    RUNNING,
    COMPLETE,
    ABORTED
}

enum WFRights {
    INIT,       // Start Workflow by submitting 1st document set version
    APPROVE,    // Approve the current document set version
    REVIEW,     // Submit an updated document set
    SIGNOFF,    // Conclude workflow successfully
    ABORT       // Abort workflow
}

interface IStateEngine {
    function getTotalStates() external view returns (uint32);
    function getTotalEdges(uint32 stateid) external view returns (uint32);
    function getTotalRights(uint32 stateid, uint32 edgeid) external view returns (uint32);
    function hasRight(uint32 state1, uint32 state2, address user, WFRights right) external view returns (bool);
    function getRight(uint32 stateid, uint32 edgeid, uint32 rightid) external view returns(address user, WFRights right);
}
