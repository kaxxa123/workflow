pragma solidity ^0.6.0;

import "./Interfaces.sol";

/// @title A contract to manage a Workflow aka Documents going through a state engine driven review process.
/// @author Alexander Zammit
contract Workflow {
    uint32 constant FLAG_REQUIRED = 1;
    uint32 constant FLAG_PUBLIC = 2;

    //Properties describing a document set element
    struct DocProps {
        uint32 flags;           //properties: public, required
        int32 loLimit;          //min number of doc instances
        int32 hiLimit;          //max number of doc instances
        int32 count;            //doc type instance count
    }

    struct HistoryInfo {
        address user;
        WFRights action;
        uint32  state;
        uint256[] idsRmv;
        uint256[] idsAdd;
        uint256[] contentAdd;
    }

    uint32 public state;                //Current State number S0, S1, S2...
    WFMode public mode;                 //Current Mode
    uint256 public wfID;                //WorkflowBuilder loan ID
    IStateEngine public engine;         //State Engine interface
    IWFRemove private addrWFMan;        //Contract with whom we have to unsubscribe WF

    //Document Set properties
    // <docType> => DocProps
    // <docType> values are automatically allocated based on the array order
    // supplied to the constructor. First array entry gets doc type 0, next 1...
    mapping(uint32 => DocProps) private docSet;
    uint32 public totalDocTypes;

    //Latest file info.
    // id => docHash
    //  where
    //         id = <uid><docType>
    //      <uid> = A uinique, version and format indepdent file id.
    //              It's up to the caller to decide how to generate this.
    //  <docType> = Doc type matching the docSet key
    //    docHash = keccak256("File content....") returns bytes32
    mapping(uint256 => uint256) public latest;

    //Document change history
    HistoryInfo[] private history;

    /// @dev Initilize workflow
    /// @param eng address of state engine contract for the workflow to follow
    /// @param docs document set that will traverse this workflow
    /// Entries are encoded as follows:
    /// <free><flags><hiLimit><loLimit>
    /// @param addrWFRmv address with whom we have to unsubscribe WF
    /// @param id Manager WF unique id
    constructor (
        address eng, 
        uint256[] memory docs,
        address addrWFRmv,
        uint256 id
    ) 
        public 
    {
        require(eng != address(0x0), "Workflow: Invalid State Engine address");
        require(addrWFRmv != address(0x0), "Workflow: Invalid WorkflowManager address");
        require(docs.length > 0, "Workflow: Empty Doc Set");

        state = 0;
        mode = WFMode.UNINIT;
        engine = IStateEngine(eng);
        addrWFMan = IWFRemove(addrWFRmv);
        wfID = id;
        initDocSet(docs);
    }

    /// @dev Perform first state transition moving the WF from unintialized to initialized
    /// @param usn identifies the expected sequence for this WF operation. Should match totalHistory()
    /// @param nextState new state id
    /// @param ids a list of document ids to initialize the WF with
    /// @param content a list of document hashes to initialize the WF with
    function doInit(uint256 usn, uint32 nextState, uint256[] calldata ids, uint256[] calldata content) external {
        require(usn == history.length,"Workflow: USN mismatch");
        require(mode == WFMode.UNINIT,"Workflow: Only when UNINIT");
        require(engine.hasRight(state, nextState, msg.sender, WFRights.INIT), "Workflow: Unauthorized state crossing");

        //ids and content include the first doc version the workflow will work on
        //We need to:
        //  Validate ids to satisfy our DocProps limits
        //  Save docs to latest
        uint tot = ids.length;
        require(tot == content.length,"Workflow: ids/content array length mismatch");

        for (uint cnt = 0; cnt < tot; ++cnt) {
            uint32 docType = uint32(ids[cnt]);
            require(docType < totalDocTypes, "Workflow: Invalid doc type");
            require(content[cnt] != 0, "Workflow: Invalid doc hash");

            DocProps storage props = docSet[docType];

            //Validate: We didn't exceed the allowed doc count for this type
            //Validate: We didn't get duplicate ids
            require(props.hiLimit >= props.count+1, "Workflow: Doc type count exceeded limit");
            require(latest[ids[cnt]] == 0, "Workflow: Initializing same ID x times");

            latest[ids[cnt]] = content[cnt];
            props.count += 1;
        }

        validateLoLimits();

        history.push(HistoryInfo({
            user: msg.sender, 
            action: WFRights.INIT, 
            state: nextState, 
            idsRmv: new uint256[](0),
            idsAdd: ids, 
            contentAdd: content}));

        state = nextState;
        mode = WFMode.RUNNING;
    }

    /// @dev Approve documents allowing the WF to move to the next state
    /// @param usn identifies the expected sequence for this WF operation. Should match totalHistory()
    /// @param nextState new state id
    function doApprove(uint256 usn, uint32 nextState) external {
        require(usn == history.length,"Workflow: USN mismatch");
        require(mode == WFMode.RUNNING,"Workflow: Only when RUNNING");
        require(engine.hasRight(state, nextState, msg.sender, WFRights.APPROVE), "Workflow: Unauthorized state crossing");
        
        uint256[] memory empty;
        history.push(HistoryInfo({
            user: msg.sender, 
            action: WFRights.APPROVE, 
            state: nextState, 
            idsRmv: empty,
            idsAdd: empty, 
            contentAdd: empty}));

        state = nextState;
    }

    /// @dev Submit document updates including adding, updating and removal of docs
    /// @param usn identifies the expected sequence for this WF operation. Should match totalHistory()
    /// @param idsRmv list of ids for docs to remove
    /// @param idsAdd list of ids for docs to add/update
    /// @param contentAdd list of doc hashes to add/update
    function doReview(uint256 usn, uint256[] calldata idsRmv, 
                       uint256[] calldata idsAdd, 
                       uint256[] calldata contentAdd) external { 

        require(usn == history.length,"Workflow: USN mismatch");
        require(mode == WFMode.RUNNING,"Workflow: Only when RUNNING");
        require(engine.hasRight(state, state, msg.sender, WFRights.REVIEW), "Workflow: Unauthorized state crossing");

        uint totRmv = idsRmv.length;
        uint totAdd = idsAdd.length;
        require((totAdd != 0) || (totRmv != 0),"Workflow: No changes submitted");
        require(totAdd == contentAdd.length,"Workflow: ids/content array length mismatch");

        //Remove documents
        for (uint cnt = 0; cnt < totRmv; ++cnt) {
            //This require effectively confirms that:
            //  1. A doc with specified id exists
            //  2. The doctype id component is valid
            //  3. props.count must be > 0 (unless we have a bug elsewhere)
            require(latest[idsRmv[cnt]] != 0,"Workflow: Doc not found");
            delete latest[idsRmv[cnt]];

            uint32 docType = uint32(idsRmv[cnt]);
            DocProps storage props = docSet[docType];
            props.count -= 1;
        }

        //Add/Update documents
        for (uint cnt = 0; cnt < totAdd; ++cnt) {
            //Get the properties for this doc type
            uint32 docType = uint32(idsAdd[cnt]);
            require(docType < totalDocTypes, "Workflow: Invalid doc type");
            require(contentAdd[cnt] != 0, "Workflow: Invalid doc hash");

            DocProps storage props = docSet[docType];

            //New Doc
            if (latest[idsAdd[cnt]] == 0) {
                //Validate that we didn't exceed the allowed doc count for this type
                require(props.hiLimit >= props.count+1, "Workflow: Doc type count exceeded limit");
                props.count += 1;
            }

            latest[idsAdd[cnt]] = contentAdd[cnt];
        }

        validateLoLimits();

        history.push(HistoryInfo({
            user: msg.sender, 
            action: WFRights.REVIEW, 
            state: state, 
            idsRmv: idsRmv,
            idsAdd: idsAdd, 
            contentAdd: contentAdd}));
    }

    /// @dev Conclude WF with a successful sign-off
    /// @param usn identifies the expected sequence for this WF operation. Should match totalHistory()
    /// @param nextState new state id
    function doSignoff(uint256 usn, uint32 nextState) external {
        require(usn == history.length,"Workflow: USN mismatch");
        require(mode == WFMode.RUNNING,"Workflow: Only when RUNNING");
        require(engine.hasRight(state, nextState, msg.sender, WFRights.SIGNOFF), "Workflow: Unauthorized state crossing");

        uint256[] memory empty;
        history.push(HistoryInfo({
            user: msg.sender, 
            action: WFRights.SIGNOFF, 
            state: nextState, 
            idsRmv: empty,
            idsAdd: empty, 
            contentAdd: empty}));

        state = nextState;
        mode = WFMode.COMPLETE;
        addrWFMan.removeWF(wfID);
    }

    /// @dev Abort WF
    /// @param usn identifies the expected sequence for this WF operation. Should match totalHistory()
    /// @param nextState new state id
    function doAbort(uint256 usn, uint32 nextState) external {
        require(usn == history.length,"Workflow: USN mismatch");
        require(mode == WFMode.RUNNING,"Workflow: Only when RUNNING");
        require(engine.hasRight(state, nextState, msg.sender, WFRights.ABORT), "Workflow: Unauthorized state crossing");

        uint256[] memory empty;
        history.push(HistoryInfo({
            user: msg.sender, 
            action: WFRights.ABORT, 
            state: nextState, 
            idsRmv: empty,
            idsAdd: empty, 
            contentAdd: empty}));

        state = nextState;
        mode = WFMode.ABORTED;
        addrWFMan.removeWF(wfID);
    }

    /// @dev Get doc type properties
    /// @param docType document type id
    /// @return flags loLimit hiLimit count
    function getDocProps(uint32 docType) external view  returns(uint32 flags, int32 loLimit, int32 hiLimit, int32 count) {
        require(docType < totalDocTypes, "Workflow: Invalid Doc Type");

        DocProps storage docProps = docSet[docType];
        return (docProps.flags, docProps.loLimit, docProps.hiLimit, docProps.count);
    }

    /// @dev Get document history by index
    /// @param idx document history index. Where index 0 is the 1st document submission
    /// operation, index 1 is the 2nd WF operation etc.
    /// @return user action stateNow idsRmv idsAdd contentAdd
    function getHistory(uint256 idx) external view returns( address user, 
                                                            WFRights action, 
                                                            uint32  stateNow, 
                                                            uint256[] memory idsRmv, 
                                                            uint256[] memory idsAdd, 
                                                            uint256[] memory contentAdd) {
        require(idx < history.length, "Workflow: Invalid Doc Type");

        HistoryInfo storage info = history[idx];

        return (info.user,
                info.action,
                info.state,
                info.idsRmv,
                info.idsAdd,
                info.contentAdd);
    }

    /// @dev Get total history elements or use as a USN to detect WF changes.
    /// @return total number of history elements
    function totalHistory() external view returns(uint256) {
        return history.length;
    }

    // Convert input document set to DocProps stuctures
    function initDocSet(uint256[] memory docs) private {
        uint32 uTot = uint32(docs.length);
        for (uint32 uCnt = 0; uCnt< uTot; ++uCnt)
        {
            // Entries are encoded as follows:
            // MSB.........................LSB
            // <free><flags><hiLimit><loLimit>
            DocProps storage oneDoc = docSet[uCnt];
            oneDoc.loLimit = int32(docs[uCnt]);
            oneDoc.hiLimit = int32(docs[uCnt] >> 32);
            oneDoc.flags   = uint32(docs[uCnt] >> 64);
        }

        totalDocTypes = uTot;
    }

    // Validate the document properties making sure that
    // the document count doesn't go below the lower limit
    function validateLoLimits() private view {
        //Validate that each required doc type is matching the minimum limit
        uint tot = totalDocTypes;
        for (uint32 cnt = 0; cnt < tot; ++cnt) {

            //It is only ok to be below the count low Limit if:
            // 1. Doc is NOT Required AND
            // 2. Count is Zero
            DocProps storage props = docSet[cnt];
            if (props.count < props.loLimit) {
                require((props.flags&FLAG_REQUIRED == 0) && (props.count == 0),"Workflow: Required files missing");
            }
        }
    }
}
