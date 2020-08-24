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
    function hasRight(uint32 state1, uint32 state2, address user, WFRights right) external view returns (bool);
}

interface IWFRemove {
    function removeWF(uint256 idx) external;
}