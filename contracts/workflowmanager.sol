pragma solidity ^0.6.0;

contract WorkflowManager {

    struct ListElement {
        uint256 prev;           //Previous list item pointer
        uint256 next;           //Next list item pointer
        address addr;           //Item data
    }

    //OWF = Open WorkFlow
    uint256 public nextOWF;    //Id of next OWF to be created, Ids are assigned sequentially
    uint256 public totalOWFs;  //Number of OWFs

    //OWF storage. id => ListElement
    mapping(uint256 => ListElement) private openWFs;
    address[] public closedWFs;

    address private owner;

    //Events
    event EventOWFAdded(uint256 indexed id, address addr);
    event EventOWFDeleted(uint256 indexed id, address addr);

    /// @dev Initializes WorkflowManager
    constructor() public {
        //Id zero is reserved.
        //First OWF to be created will have id 1
        nextOWF = 1;
        owner = msg.sender;
    }

    /// @dev Add OWF.
    /// @param addr OWF data
    function addOWF(address addr) external {
        require(owner == msg.sender, "Unauthorized");
        require(addr != address(0x0), "Address cannot be zero");

        ListElement storage prevElem = openWFs[lastOWF()];
        ListElement storage newElem = openWFs[nextOWF];

        prevElem.next = nextOWF;
        newElem.prev = lastOWF();
        newElem.addr = addr;

        lastOWFSet(nextOWF);
        nextOWF += 1;
        totalOWFs += 1;

        emit EventOWFAdded(lastOWF(), addr);
    }

    /// @dev Remove OWF with given id
    /// @param id element to be removed
    function removeOWF(uint256 id) external {
        require(owner == msg.sender, "Unauthorized");

        //Make sure we are deleting a valid OWF i.e. an OWF that is actually in the list
        //Note this will also block deleting the root node (id == 0)
        ListElement storage elem = openWFs[id];
        require(elem.addr != address(0x0), "Uninitialized OWF cannot be deleted");

        //Add log for deleted OWF
        emit EventOWFDeleted(id, elem.addr);

        //Update the previous list entry
        ListElement storage prevElem = openWFs[elem.prev];
        prevElem.next = elem.next;

        //If deleted elem was the list tail than update lastOWF
        //to point at the prev element
        if (elem.next == 0) {
            lastOWFSet(elem.prev);
        }
        else {
            ListElement storage nextElem = openWFs[elem.next];
            nextElem.prev = elem.prev;
        }

        //Delete element
        closedWFs.push(elem.addr);
        delete openWFs[id];
        totalOWFs -= 1;
    }

    /// @dev Reads up to <toRead> OWFs starting from the element with id start
    /// It is up to the caller to make sure that he doesn't ask for too many
    /// OWFs such that to run out of memory
    /// @param start specify the starting id for reading OWFs. start = 0 => list head
    /// @param toRead number of OWFs to read. toRead = 0 => read all OWFs up to the end
    /// @return addrList - an array of OWF data, next - next reading position (next == 0 indicates we reached the list end)
    function readOWF(uint256 start, uint256 toRead) external view returns (address[] memory addrList, uint256 next) {

        //start = 0 => list head
        uint256 itemPos;
        if (start == 0)
             itemPos = firstOWF();
        else itemPos = start;

        //Cater for the special case where the list is empty
        if (itemPos == 0)
            return (new address[](0), 0);

        //Validate reading position
        //This can happen if an OWF is deleted while traversing the list
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

        //Return requested OWFs
        addrList = new address[](size);

        if (start == 0)
             itemPos = firstOWF();
        else itemPos = start;

        uint256 arrayPos;
        while (arrayPos < size) {
           addrList[arrayPos] = openWFs[itemPos].addr;

           arrayPos += 1;
           itemPos = openWFs[itemPos].next;
        }

        next = itemPos;
    }

    /// @dev Get id of first OWF in list
    /// @return id of first OWF
    function firstOWF() public view returns (uint256) {
        return openWFs[0].next;
    }

    /// @dev Get id of last OWF in list
    /// @return id of last OWF
    function lastOWF() public view returns (uint256) {
        return openWFs[0].prev;
    }

    /// @dev Set id of last OWF added
    /// @param id id value to set
    function lastOWFSet(uint256 id) private {
        openWFs[0].prev = id;
    }
}