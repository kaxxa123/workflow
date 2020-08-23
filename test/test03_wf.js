const WorkflowBuilder = artifacts.require("WorkflowBuilder");
const Workflow = artifacts.require("Workflow");
const HlpFail = require('./helpers/testFailure');
const WFRights = require('./helpers/wfhelp').WFRights;
const WFMode = require('./helpers/wfhelp').WFMode;
const makeDocID = require('./helpers/wfhelp').makeDocID;
const getLatest = require('./helpers/wfhelp').getLatest;

const showDocSet = async (wf) => {
    let totDocs = await wf.totalDocTypes()

    for (cnt = 0; cnt <totDocs; ++cnt) {
        let docProps = await wf.getDocProps(cnt);
        console.log(`Flags: ${docProps.flags}, loLimit: ${docProps.loLimit}, hiLimit: ${docProps.hiLimit}, count: ${docProps.count}`);
    }
}

const toHex = (item) => `0x${item.toString(16)}`;

const showHistory = async (wf) => {
    let totHist = await wf.totalHistory()
    console.log();

    for (cnt = 0; cnt <totHist; ++cnt) {
        let history = await wf.getHistory(cnt);
        console.log(`user: ${history.user}, action: ${history.action}`);
        console.log(`stateNow: ${history.stateNow}`);
        console.log(`Removed: ${history.idsRmv.map(toHex)}`);
        console.log(`Added: ${history.idsAdd.map(toHex)} => ${history.contentAdd.map(toHex)}`);
        console.log();
    }
}

const showLatest = async (wf) => {

    let docIds = await getLatest(wf);
    console.log();
    console.log(`Current document ids:`);
    console.log(`${docIds.map(toHex)}`);
    console.log();
}

contract('Testing Workflow', function (accounts) {

    it('Should Create Workflow Engine OK', async () => {

        let engine = await WorkflowBuilder.deployed();
        let wf = await Workflow.deployed();
        let engineAddr = await wf.engine()
        assert(engineAddr == engine.address, "Mismatched engine address")

        await engine.addState([1]);
        await engine.addState([2]);
        await engine.addState([3]);
        await engine.addState([1,2,3,4,5]);
        await engine.addState([]);
        await engine.addState([]);
        await engine.doFinalize();
        await engine.addRight(0, 0, accounts[1], WFRights.INIT);       //S0 -> S1
        await engine.addRight(1, 0, accounts[1], WFRights.APPROVE);    //S1 -> S2
        await engine.addRight(1, 0, accounts[2], WFRights.APPROVE);    //S1 -> S2
        await engine.addRight(2, 0, accounts[2], WFRights.APPROVE);    //S2 -> S3
        await engine.addRight(3, 0, accounts[1], WFRights.APPROVE);    //S3 -> S1
        await engine.addRight(3, 1, accounts[1], WFRights.APPROVE);    //S3 -> S2
        await engine.addRight(3, 2, accounts[1], WFRights.REVIEW);     //S3 -> S3
        await engine.addRight(3, 3, accounts[1], WFRights.SIGNOFF);    //S3 -> S4
        await engine.addRight(3, 4, accounts[1], WFRights.ABORT);      //S3 -> S5
    });

    it('Should fail to init WF', async () => {

        let wf = await Workflow.deployed();
        let state = await wf.state()
        let mode = await wf.mode()
        let totDocs = await wf.totalDocTypes()
        let totHist = await wf.totalHistory()

        assert(state == 0, "Should be State 0");
        assert(mode == WFMode.UNINIT, "Should be UNINT State");
        assert(totDocs == 4, "Total docs should be 4");
        assert(totHist == 0, "Total history should be 0");

        await showDocSet(wf)

        //Sender has no Right to perform this action
        await HlpFail.testFail("wf.doInit", "Unauthorized state crossing", async () => { 
            await wf.doInit(1, [makeDocID(doctype=0,id=0), makeDocID(1, 1)], [0x111,0x112]) 
        });

        //Invalid parameters - ids array longer the content array
        await HlpFail.testFail("wf.doInit", "ids/content array length mismatch", async () => { 
            await wf.doInit(1, [makeDocID(0, 230), makeDocID(1, 124), makeDocID(2, 123)], [0x111,0x112], {from: accounts[1]}) 
        });

        //DocType=5 is invalid
        await HlpFail.testFail("wf.doInit", "Invalid doc type", async () => { 
            await wf.doInit(1, [makeDocID(1, 111), makeDocID(5, 151)], [0x111,0x112], {from: accounts[1]}) 
        });

        //Doc hash cannot be zero
        await HlpFail.testFail("wf.doInit", "Invalid doc hash", async () => { 
            await wf.doInit(1, [makeDocID(0, 100), makeDocID(1, 221)], [0x111,0], {from: accounts[1]}) 
        });

        //Cannot have 3 documents of type 2 (2 is upper limit)
        await HlpFail.testFail("wf.doInit", "Doc type count exceeded limit", async () => { 
            await wf.doInit(1, [makeDocID(2, 111), makeDocID(2, 222), makeDocID(2, 333)], [0x111,0x112,0x113], {from: accounts[1]}) 
        });

        //Cannot add 2 docs with same id
        await HlpFail.testFail("wf.doInit", "Initializing same ID x times", async () => { 
            await wf.doInit(1, [makeDocID(0, 100), makeDocID(1, 101), makeDocID(1, 101)], [0x111,0x112,0x113], {from: accounts[1]}) 
        });

        //DocType 1 is marked as required but it's not included in doInit
        await HlpFail.testFail("wf.doInit", "Required files missing", async () => { 
            await wf.doInit(1, [makeDocID(0, 100), makeDocID(2, 101)], [0x111,0x112], {from: accounts[1]})
        });

        //DocType 3 is not required. However it has a minimum set to 2. However doInit is only supplied with 1 such docType
        //When a DocType is not required we can either have Zero or at least match the minimum limit.
        await HlpFail.testFail("wf.doInit", "Required files missing", async () => { 
            await wf.doInit(1, [makeDocID(0, 1000), makeDocID(1, 101), makeDocID(3, 101)], [0x111,0x112,0x113], {from: accounts[1]})
        });
    });

    it('Should fail to perform any action other than Init', async () => {
        let wf = await Workflow.deployed();

        //From S0 only a doInit is allowed
        await HlpFail.testFail("wf.doApprove", "Unauthorized state crossing", async () => { 
            await wf.doApprove(1, {from: accounts[1]})
        });

        //From S15 does not exist
        await HlpFail.testFail("wf.doApprove", "Non-existing state", async () => { 
            await wf.doApprove(15, {from: accounts[1]})
        });

        //From S0 only a doInit is allowed
        await HlpFail.testFail("wf.doReview", "Unauthorized state crossing", async () => { 
            await wf.doReview([1],[1],[1], {from: accounts[1]})
        });

        //From S0 only a doInit is allowed
        await HlpFail.testFail("wf.doSignoff", "Unauthorized state crossing", async () => { 
            await wf.doSignoff(1, {from: accounts[1]})
        });

        //From S15 does not exist
        await HlpFail.testFail("wf.doSignoff", "Non-existing state", async () => { 
            await wf.doSignoff(15, {from: accounts[1]})
        });

        //From S0 only a doInit is allowed
        await HlpFail.testFail("wf.doAbort", "Unauthorized state crossing", async () => { 
            await wf.doAbort(1, {from: accounts[1]})
        });

        //From S15 does not exist
        await HlpFail.testFail("wf.doAbort", "Non-existing state", async () => { 
            await wf.doAbort(15, {from: accounts[1]})
        });
    });

    it('Should perform Init Ok', async () => {
        let wf = await Workflow.deployed();

        await wf.doInit(1, [makeDocID(0, 1000), makeDocID(1, 101)], [0x111,0x112], {from: accounts[1]})

        let state = await wf.state()
        let mode = await wf.mode()
        let totDocs = await wf.totalDocTypes()
        let totHist = await wf.totalHistory()

        assert(state == 1, "Should be State 1");
        assert(mode == WFMode.RUNNING, "Should be RUNNING State");
        assert(totDocs == 4, "Total docs should be 4");
        assert(totHist == 1, "Total history should be 1");

        await showHistory(wf);
        await showLatest(wf);
    });

    it('Should fail to Approve WF', async () => {

        let wf = await Workflow.deployed();

        //Sender has no Right to perform this action
        await HlpFail.testFail("wf.doApprove", "Unauthorized state crossing", async () => { 
            await wf.doApprove(2) 
        });

        //No edge between S1 and S3
        await HlpFail.testFail("wf.doApprove", "Unauthorized state crossing", async () => { 
            await wf.doApprove(3, {from: accounts[1]}) 
        });

        //S15 does not exist
        await HlpFail.testFail("wf.doApprove", "Non-existing state", async () => { 
            await wf.doApprove(15, {from: accounts[1]}) 
        });
    });

    it('Should fail to perform any action other than Approve', async () => {

        let wf = await Workflow.deployed();

        //From S1 only only an Approve action is configured
        await HlpFail.testFail("wf.doInit", "Unauthorized state crossing", async () => { 
            await wf.doInit(1, [makeDocID(0, 1000), makeDocID(1, 101)], [0x111,0x112], {from: accounts[1]})
        });

        //S15 does not exist
        await HlpFail.testFail("wf.doInit", "Non-existing state", async () => { 
            await wf.doInit(15, [makeDocID(0, 1000), makeDocID(1, 101)], [0x111,0x112], {from: accounts[1]})
        });

        //From S1 only only an Approve action is configured
        await HlpFail.testFail("wf.doReview", "Unauthorized state crossing", async () => { 
            await wf.doReview([1],[1],[1], {from: accounts[1]})
        });

        //From S1 only only an Approve action is configured
        await HlpFail.testFail("wf.doSignoff", "Unauthorized state crossing", async () => { 
            await wf.doSignoff(1, {from: accounts[1]})
        });

        //S15 does not exist
        await HlpFail.testFail("wf.doSignoff", "Non-existing state", async () => { 
            await wf.doSignoff(15, {from: accounts[1]})
        });

        //From S1 only only an Approve action is configured
        await HlpFail.testFail("wf.doAbort", "Unauthorized state crossing", async () => { 
            await wf.doAbort(1, {from: accounts[1]})
        });

        //S15 does not exist
        await HlpFail.testFail("wf.doAbort", "Non-existing state", async () => { 
            await wf.doAbort(15, {from: accounts[1]})
        });
    });

    it('Should Cross State to Apporve Ok', async () => {
        let wf = await Workflow.deployed();

        //S1 -> S2
        await wf.doApprove(2, {from: accounts[1]})

        let state = await wf.state()
        let mode = await wf.mode()
        let totDocs = await wf.totalDocTypes()
        let totHist = await wf.totalHistory()
        assert(state == 2, "Should be State 1");
        assert(mode == WFMode.RUNNING, "Should be RUNNING State");
        assert(totDocs == 4, "Total docs should be 4");
        assert(totHist == 2, "Total history should be 1");

        //S2 -> S3
        await wf.doApprove(3, {from: accounts[2]})

        state = await wf.state()
        mode = await wf.mode()
        totDocs = await wf.totalDocTypes()
        totHist = await wf.totalHistory()
        assert(state == 3, "Should be State 1");
        assert(mode == WFMode.RUNNING, "Should be RUNNING State");
        assert(totDocs == 4, "Total docs should be 4");
        assert(totHist == 3, "Total history should be 1");

        //S3 -> S1 -> S2
        await wf.doApprove(1, {from: accounts[1]})
        await wf.doApprove(2, {from: accounts[1]})

        state = await wf.state()
        mode = await wf.mode()
        totDocs = await wf.totalDocTypes()
        totHist = await wf.totalHistory()

        assert(state == 2, "Should be State 1");
        assert(mode == WFMode.RUNNING, "Should be RUNNING State");
        assert(totDocs == 4, "Total docs should be 4");
        assert(totHist == 5, "Total history should be 1");
        await showHistory(wf);
        await showLatest(wf);
    });

    it('Should fail to Approve WF', async () => {

        let wf = await Workflow.deployed();

        //Sender has no Right to perform this action
        await HlpFail.testFail("wf.doApprove", "Unauthorized state crossing", async () => { 
            await wf.doApprove(2) 
        });

        //Edge between S2 to S4 does not exist
        await HlpFail.testFail("wf.doApprove", "Unauthorized state crossing", async () => { 
            await wf.doApprove(4, {from: accounts[2]}) 
        });

        //S15 does not exist
        await HlpFail.testFail("wf.doApprove", "Non-existing state", async () => { 
            await wf.doApprove(15, {from: accounts[2]}) 
        });
    });

    it('Should fail to perform any action other than Approve', async () => {

        let wf = await Workflow.deployed();

        //From S2 only only an Approve action is configured
        await HlpFail.testFail("wf.doInit", "Unauthorized state crossing", async () => { 
            await wf.doInit(1, [makeDocID(0, 1000), makeDocID(1, 101)], [0x111,0x112], {from: accounts[2]})
        });

        //S15 does not exist
        await HlpFail.testFail("wf.doInit", "Non-existing state", async () => { 
            await wf.doInit(15, [makeDocID(0, 1000), makeDocID(1, 101)], [0x111,0x112], {from: accounts[2]})
        });

        //From S2 only only an Approve action is configured
        await HlpFail.testFail("wf.doReview", "Unauthorized state crossing", async () => { 
            await wf.doReview([1],[1],[1], {from: accounts[2]})
        });

        //From S2 only only an Approve action is configured
        await HlpFail.testFail("wf.doSignoff", "Unauthorized state crossing", async () => { 
            await wf.doSignoff(1, {from: accounts[2]})
        });

        //S15 does not exist
        await HlpFail.testFail("wf.doSignoff", "Non-existing state", async () => { 
            await wf.doSignoff(15, {from: accounts[2]})
        });

        //From S2 only only an Approve action is configured
        await HlpFail.testFail("wf.doAbort", "Unauthorized state crossing", async () => { 
            await wf.doAbort(1, {from: accounts[2]})
        });

        //S15 does not exist
        await HlpFail.testFail("wf.doAbort", "Non-existing state", async () => { 
            await wf.doAbort(15, {from: accounts[2]})
        });
    });

    it('Should Cross State to Apporve Ok2', async () => {
        let wf = await Workflow.deployed();

        //S2 -> S3 -> S2
        await wf.doApprove(3, {from: accounts[2]})
        await wf.doApprove(2, {from: accounts[1]})
        await wf.doApprove(3, {from: accounts[2]})

        state = await wf.state()
        mode = await wf.mode()
        totDocs = await wf.totalDocTypes()
        totHist = await wf.totalHistory()
        assert(state == 3, "Should be State 1");
        assert(mode == WFMode.RUNNING, "Should be RUNNING State");
        assert(totDocs == 4, "Total docs should be 4");
        assert(totHist == 8, "Total history should be 1");

        await showHistory(wf);
        await showLatest(wf);
    });

    it('Should fail to Review WF', async () => {

        let wf = await Workflow.deployed();

        //Sender has no Right to perform this action
        await HlpFail.testFail("wf.doReview", "Unauthorized state crossing", async () => { 
            await wf.doReview([makeDocID(1, 101)], [makeDocID(1, 2000), makeDocID(1, 2001)], [0x111,0x112], {from: accounts[0]});
        });

        //Review action is not submitting any updates
        await HlpFail.testFail("wf.doReview", "No changes submitted", async () => { 
            await wf.doReview([], [], [], {from: accounts[1]});
        });

        //idsAdd and contentAdd arrays must have a matching length
        await HlpFail.testFail("wf.doReview", "ids/content array length mismatch", async () => { 
            await wf.doReview([makeDocID(1, 101)], [makeDocID(1, 2000), makeDocID(1, 2001)], [0x111,0x112,0x113], {from: accounts[1]});
        });

        //Cannot remove non-existing id: makeDocID(9, 101)
        await HlpFail.testFail("wf.doReview", "Doc not found", async () => { 
            await wf.doReview([makeDocID(9, 101)], [makeDocID(1, 2000), makeDocID(1, 2001)], [0x111,0x112], {from: accounts[1]});
        });

        //Cannot add doc with unknown docType: makeDocID(9, 2000)
        await HlpFail.testFail("wf.doReview", "Invalid doc type", async () => { 
            await wf.doReview([makeDocID(1, 101)], [makeDocID(9, 2000), makeDocID(1, 2001)], [0x111,0x112], {from: accounts[1]});
        });

        //Cannot add/update with invalid hash
        await HlpFail.testFail("wf.doReview", "Invalid doc hash", async () => { 
            await wf.doReview([makeDocID(1, 101)], [makeDocID(1, 2000), makeDocID(1, 2001)], [0x111,0], {from: accounts[1]});
        });

        //docType=1 allows for max 2 documents
        await HlpFail.testFail("wf.doReview", "Doc type count exceeded limit", async () => { 
            await wf.doReview([makeDocID(1, 101)], [makeDocID(1, 2000), makeDocID(1, 2001), makeDocID(1, 2002)], [0x111,0x112,0x113], {from: accounts[1]});
        });

        //Removing required docType 0
        await HlpFail.testFail("wf.doReview", "Required files missing", async () => { 
            await wf.doReview([makeDocID(0, 1000)], [], [], {from: accounts[1]});
        });

        //docType 3 has a minimum of 2. Cannot add just one.
        await HlpFail.testFail("wf.doReview", "Required files missing", async () => { 
            await wf.doReview([], [makeDocID(3, 1000)], [0x1111], {from: accounts[1]});
        });
    });

    it('Should Review OK', async () => {
        let wf = await Workflow.deployed();

        await wf.doReview([makeDocID(1, 101)], [makeDocID(1, 2000), makeDocID(1, 2001)], [0x111,0x112], {from: accounts[1]});
        await showHistory(wf);
        await showLatest(wf);
    });

});
