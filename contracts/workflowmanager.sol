pragma solidity ^0.6.0;

import "./workflow.sol";
import "@openzeppelin/contracts/access/AccessControl.sol";

/// @title A contract to manage a list of WFs
/// @author Alexander Zammit
contract WorkflowManager is IWFRemove, AccessControl {

    bytes32 public constant WF_ADMIN_ROLE = keccak256("WF_ADMIN_ROLE");

    struct ListElement {
        uint256 prev;           //Previous list item pointer
        uint256 next;           //Next list item pointer
        address addr;           //Item data
    }

    uint256 public nextWF;    //Id of next WF to be created, Ids are assigned sequentially
    uint256 public totalWFs;  //Number of WFs

    //WF storage. id => ListElement
    mapping(uint256 => ListElement) private openWFs;
    address[] public closedWFs;

    //Events
    event EventWFAdded(uint256 indexed id, address addr);
    event EventWFDeleted(uint256 indexed id, address addr);

    /// @dev Initializes WorkflowManager
    constructor() public {
        //Id zero is reserved.
        //First WF to be created will have id 1
        nextWF = 1;

        _setupRole(DEFAULT_ADMIN_ROLE, msg.sender);
    }

    /// @dev Add WF.
    /// @param eng state engine for the workflow to follow
    /// @param docs document set that will traverse this workflow
    /// Entries are encoded as follows:
    /// <free><flags><hiLimit><loLimit>
    function addWF(address eng, uint256[] calldata docs) external {
        require(hasRole(WF_ADMIN_ROLE, msg.sender), "Unauthorized");

        Workflow oneWF = new Workflow(
            eng, 
            docs,
            address(this),
            nextWF);

        ListElement storage prevElem = openWFs[lastWF()];
        ListElement storage newElem = openWFs[nextWF];

        prevElem.next = nextWF;
        newElem.prev = lastWF();
        newElem.addr = address(oneWF);

        lastWFSet(nextWF);
        nextWF += 1;
        totalWFs += 1;

        emit EventWFAdded(lastWF(), address(oneWF));
    }

    /// @dev Remove WF with given id
    /// @param id element to be removed
    function removeWF(uint256 id) override external {

        //Make sure we are deleting a WF that is actually in the list
        //Note this will also block deleting the root node (id == 0)
        ListElement storage elem = openWFs[id];
        require(elem.addr != address(0x0), "Uninitialized WF cannot be deleted");

        //Confirm that WF is ready for delisting
        Workflow oneWF = Workflow(elem.addr);
        WFMode mode = oneWF.mode();
        require((mode == WFMode.COMPLETE) || (mode == WFMode.ABORTED), "WF still not concluded");

        //Add log for deleted WF
        emit EventWFDeleted(id, elem.addr);

        //Update the previous list entry
        ListElement storage prevElem = openWFs[elem.prev];
        prevElem.next = elem.next;

        //If deleted elem was the list tail than update lastWF
        //to point at the prev element
        if (elem.next == 0) {
            lastWFSet(elem.prev);
        }
        else {
            ListElement storage nextElem = openWFs[elem.next];
            nextElem.prev = elem.prev;
        }

        //Delete element
        closedWFs.push(elem.addr);
        delete openWFs[id];
        totalWFs -= 1;
    }

    /// @dev Reads up to <toRead> WFs starting from the element with id start
    /// It is up to the caller to make sure that he doesn't ask for too many
    /// WFs such that to run out of memory
    /// @param start specify the starting id for reading WFs. start = 0 => list head
    /// @param toRead number of WFs to read. toRead = 0 => read all WFs up to the end
    /// @return addrList - an array of WF data, next - next reading position (next == 0 indicates we reached the list end)
    function readWF(uint256 start, uint256 toRead) external view returns (address[] memory addrList, uint256 next) {

        //start = 0 => list head
        uint256 itemPos;
        if (start == 0)
             itemPos = firstWF();
        else itemPos = start;

        //Cater for the special case where the list is empty
        if (itemPos == 0)
            return (new address[](0), 0);

        //Validate reading position
        //This can happen if a WF is deleted while traversing the list
        require(openWFs[itemPos].addr != address(0x0), "Invalid reading position.");

        //Determine number of elements to be returned
        //We count until:
        // Hitting the root OR
        // toRead elements are traversed
        uint256 size = 0;
        while ((itemPos != 0) && ((toRead == 0) || (size < toRead))) {
           size += 1;
           itemPos = openWFs[itemPos].next;
        }

        //Return requested WFs
        addrList = new address[](size);

        if (start == 0)
             itemPos = firstWF();
        else itemPos = start;

        uint256 arrayPos;
        while (arrayPos < size) {
           addrList[arrayPos] = openWFs[itemPos].addr;

           arrayPos += 1;
           itemPos = openWFs[itemPos].next;
        }

        next = itemPos;
    }

    /// @dev Get id of first WF in list
    /// @return id of first WF
    function firstWF() public view returns (uint256) {
        return openWFs[0].next;
    }

    /// @dev Get id of last WF in list
    /// @return id of last WF
    function lastWF() public view returns (uint256) {
        return openWFs[0].prev;
    }

    /// @dev Get the total closed WF entries
    /// @return total count of closed WFs
    function totalClosedWFs() external view returns (uint256) {
        return closedWFs.length;
    }

    /// @dev Set id of last WF added
    /// @param id id value to set
    function lastWFSet(uint256 id) private {
        openWFs[0].prev = id;
    }
}