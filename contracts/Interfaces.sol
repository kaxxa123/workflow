pragma solidity ^0.6.0;

interface IWFRights {
    function hasRight(uint32 nowState, address uid, uint32 right) external view returns (uint256);
    function getParticipants() external view returns (uint256);
}
