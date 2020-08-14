pragma solidity ^0.6.0;

import "./Interfaces.sol";

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

    struct DocInfo {
        uint256 hashContent;    //keccak256("File content....") returns bytes32
        uint32 version;         //usn
    }

    uint32 public state;
    WFMode public mode;
    IStateEngine public engine;

    //Document Set properties
    // <docType> => DocProps
    // <docType> values are automatically allocated based on the array order
    // supplied to the constructor. First array entry gets doc type 0, next 1...
    mapping(uint32 => DocProps) public docSet;
    uint32 public totalDocTypes;

    //Latest file info.
    // id => DocInfo
    //  where
    //         id = <uid><docType>
    //      <uid> = A uinique, version and format indepdent file id.
    //              It's up to the caller to decide how to generate this.
    //  <docType> = Doc type matching the docSet key
    //
    mapping(uint256 => DocInfo) public latest;

    /// @dev Initilize workflow
    /// @param eng state engine for worklow to follow
    /// @param docs document set that will traverse this workflow
    /// Entries are encoded as follows:
    /// <free><flags><hiLimit><loLimit>
    constructor (IStateEngine eng, uint256[] memory docs) public {
        state = 0;
        engine = eng;
        mode = WFMode.UNINIT;
        initDocSet(docs);
    }

    /// @dev Perform first state transition moving the WF from unintialized to initialized
    /// @param nextState new state id
    /// @param ids a list of document ids to initialize the WF with
    /// @param content a list of document hashes to initialize the WF with
    function doInit(uint32 nextState, uint256[] memory ids, uint256[] memory content) public {
        require(engine.hasRight(state, nextState, msg.sender, WFRights.INIT), "Unauthorized state crossing");
        require(mode == WFMode.UNINIT,"Only when UNINIT");

        //ids and content include the first doc version the workflow will work on
        //We need to:
        //  Validate ids to satisfy our DocProps limits
        //  Save docs to latest
        uint tot = ids.length;
        require(tot == content.length,"ids/content array length mismatch");

        for (uint cnt = 0; cnt < tot; ++cnt) {
            //Get the properties for this doc type
            uint32 docType = uint32(ids[cnt]);
            require(docType < totalDocTypes, "Invalid doc type");

            DocProps storage props = docSet[docType];
            DocInfo storage doc = latest[ids[cnt]];

            //Validate that we didn't exceed the allowed doc count for this type
            require(props.hiLimit >= props.count+1, "Doc type count exceeded limit");

            //Validate that we didn't already see this id
            require(doc.hashContent == 0, "Initializing same ID x times");

            doc.hashContent = content[cnt];
            props.count += 1;
        }

        validateLoLimits();

        state = nextState;
        mode = WFMode.RUNNING;
    }

    /// @dev Approve documents allowing the WF to move to the next state
    /// @param nextState new state id
    function doApprove(uint32 nextState) public {
        require(engine.hasRight(state, nextState, msg.sender, WFRights.APPROVE), "Unauthorized state crossing");
        require(mode == WFMode.RUNNING,"Only when RUNNING");
        state = nextState;
    }

    /// @dev Submit document updates including adding, updating and removal of docs
    /// @param idsRmv list of ids for docs to remove
    /// @param idsAdd list of ids for docs to add/update
    /// @param contentAdd list of doc hashes to add/update
    function doReview(
                uint256[] memory idsRmv, 
                uint256[] memory idsAdd, 
                uint256[] memory contentAdd) public {
        require(engine.hasRight(state, state, msg.sender, WFRights.REVIEW), "Unauthorized state crossing");
        require(mode == WFMode.RUNNING,"Only when RUNNING");

        uint totRmv = idsRmv.length;
        uint totAdd = idsAdd.length;
        require((totAdd != 0) || (totRmv != 0),"No changes submitted");
        require(totAdd == contentAdd.length,"ids/content array length mismatch");

        //Remove documents
        for (uint cnt = 0; cnt < totRmv; ++cnt) {
            require(latest[idsRmv[cnt]].hashContent != 0,"Doc not found");
            delete latest[idsRmv[cnt]];

            uint32 docType = uint32(idsRmv[cnt]);
            require(docType < totalDocTypes,"Invalid doc type");
            DocProps storage props = docSet[docType];
            
            require(props.count < 1,"Unexpected doc type count");
            props.count -= 1;
        }

        //Add/Update documents
        for (uint cnt = 0; cnt < totAdd; ++cnt) {
            //Get the properties for this doc type
            uint32 docType = uint32(idsAdd[cnt]);
            require(docType < totalDocTypes, "Invalid doc type");

            DocProps storage props = docSet[docType];
            DocInfo storage doc = latest[idsAdd[cnt]];

            //New Doc
            if (doc.hashContent == 0) {
                //Validate that we didn't exceed the allowed doc count for this type
                require(props.hiLimit >= props.count+1, "Doc type count exceeded limit");
                props.count += 1;
            }

            //Updated Doc
            doc.hashContent = contentAdd[cnt];
            doc.version += 1;
        }

        validateLoLimits();
    }

    /// @dev Conclude WF with a successful sign-off
    /// @param nextState new state id
    function doSignoff(uint32 nextState) public {
        require(engine.hasRight(state, nextState, msg.sender, WFRights.SIGNOFF), "Unauthorized state crossing");
        require(mode == WFMode.RUNNING,"Only when RUNNING");
        state = nextState;
        mode = WFMode.COMPLETE;
    }

    /// @dev Abort WF
    /// @param nextState new state id
    function doAbort(uint32 nextState) public {
        require(engine.hasRight(state, nextState, msg.sender, WFRights.ABORT), "Unauthorized state crossing");
        require(mode == WFMode.RUNNING,"Only when RUNNING");
        state = nextState;
        mode = WFMode.ABORTED;
    }

    // Convert input document set to DocProps stuctures
    function initDocSet(uint256[] memory docs) private
    {
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
                require((props.flags&FLAG_REQUIRED == 0) && (props.count == 0),"Required files missing");
            }
        }
    }
}
