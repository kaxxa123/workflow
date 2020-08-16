const WorkflowBuilder = artifacts.require("WorkflowBuilder");
const Workflow = artifacts.require("Workflow");
const HlpFail = require('./helpers/testFailure');
const WFRights = require('./helpers/wfhelp').WFRights;
const WFMode = require('./helpers/wfhelp').WFMode;
const makeDocID = require('./helpers/wfhelp').makeDocID;

const showDocSet = async (wf) => {
    let totDocs = await wf.totalDocTypes()

    for (cnt = 0; cnt <totDocs; ++cnt) {
        let docProps = await wf.getDocProps(cnt);
        console.log(`Flags: ${docProps.flags}, loLimit: ${docProps.loLimit}, hiLimit: ${docProps.hiLimit}, count: ${docProps.count}`);
    }
}

const showHistory = async (wf) => {
    let totHist = await wf.totalHistory()
    console.log();

    for (cnt = 0; cnt <totHist; ++cnt) {
        let history = await wf.getHistory(cnt);
        console.log(`user: ${history.user}, action: ${history.action}`);
        console.log(`stateNow: ${history.stateNow}`);
        console.log(`Removed: ${history.idsRmv}`);
        console.log(`Added: ${history.idsAdd} => ${history.contentAdd}`);
        console.log();
    }
}

// const showLatest = async (wf) => {
// }

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

        await HlpFail.testFail("wf.doInit", "Unauthorized state crossing", async () => { 
            await wf.doInit(1, [makeDocID(doctype=0,id=0), makeDocID(1, 1)], [0x111,0x112]) 
        });

        await HlpFail.testFail("wf.doInit", "ids/content array length mismatch", async () => { 
            await wf.doInit(1, [makeDocID(0, 230), makeDocID(1, 124), makeDocID(2, 123)], [0x111,0x112], {from: accounts[1]}) 
        });

        await HlpFail.testFail("wf.doInit", "Invalid doc type", async () => { 
            await wf.doInit(1, [makeDocID(1, 111), makeDocID(5, 151)], [0x111,0x112], {from: accounts[1]}) 
        });

        await HlpFail.testFail("wf.doInit", "Invalid doc hash", async () => { 
            await wf.doInit(1, [makeDocID(0, 100), makeDocID(1, 221)], [0x111,0], {from: accounts[1]}) 
        });

        await HlpFail.testFail("wf.doInit", "Doc type count exceeded limit", async () => { 
            await wf.doInit(1, [makeDocID(2, 111), makeDocID(2, 222), makeDocID(2, 333)], [0x111,0x112,0x113], {from: accounts[1]}) 
        });

        await HlpFail.testFail("wf.doInit", "Initializing same ID x times", async () => { 
            await wf.doInit(1, [makeDocID(0, 100), makeDocID(1, 101), makeDocID(1, 101)], [0x111,0x112,0x113], {from: accounts[1]}) 
        });

        await HlpFail.testFail("wf.doInit", "Required files missing", async () => { 
            await wf.doInit(1, [makeDocID(0, 100), makeDocID(2, 101)], [0x111,0x112], {from: accounts[1]})
        });

        await HlpFail.testFail("wf.doInit", "Required files missing", async () => { 
            await wf.doInit(1, [makeDocID(0, 1000), makeDocID(1, 101), makeDocID(3, 101)], [0x111,0x112,0x113], {from: accounts[1]})
        });
    });

    it('Should fail to perform any action other than Init', async () => {
        let wf = await Workflow.deployed();

        await HlpFail.testFail("wf.doApprove", "Unauthorized state crossing", async () => { 
            await wf.doApprove(1, {from: accounts[1]})
        });

        await HlpFail.testFail("wf.doApprove", "Non-existing state", async () => { 
            await wf.doApprove(15, {from: accounts[1]})
        });

        await HlpFail.testFail("wf.doReview", "Unauthorized state crossing", async () => { 
            await wf.doReview([1],[1],[1], {from: accounts[1]})
        });

        await HlpFail.testFail("wf.doSignoff", "Unauthorized state crossing", async () => { 
            await wf.doSignoff(1, {from: accounts[1]})
        });

        await HlpFail.testFail("wf.doSignoff", "Non-existing state", async () => { 
            await wf.doSignoff(15, {from: accounts[1]})
        });

        await HlpFail.testFail("wf.doAbort", "Unauthorized state crossing", async () => { 
            await wf.doAbort(1, {from: accounts[1]})
        });

        await HlpFail.testFail("wf.doAbort", "Non-existing state", async () => { 
            await wf.doAbort(15, {from: accounts[1]})
        });

    });

    it('Should Cross State to Init Ok', async () => {
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
    });

    it('Should fail to Approve WF', async () => {

        let wf = await Workflow.deployed();

        await HlpFail.testFail("wf.doApprove", "Unauthorized state crossing", async () => { 
            await wf.doApprove(2) 
        });

        await HlpFail.testFail("wf.doApprove", "Unauthorized state crossing", async () => { 
            await wf.doApprove(3, {from: accounts[1]}) 
        });

        await HlpFail.testFail("wf.doApprove", "Non-existing state", async () => { 
            await wf.doApprove(15, {from: accounts[1]}) 
        });
    });

    it('Should fail to perform any action other than Approve', async () => {

        let wf = await Workflow.deployed();

        await HlpFail.testFail("wf.doInit", "Unauthorized state crossing", async () => { 
            await wf.doInit(1, [makeDocID(0, 1000), makeDocID(1, 101)], [0x111,0x112], {from: accounts[1]})
        });

        await HlpFail.testFail("wf.doInit", "Non-existing state", async () => { 
            await wf.doInit(15, [makeDocID(0, 1000), makeDocID(1, 101)], [0x111,0x112], {from: accounts[1]})
        });

        await HlpFail.testFail("wf.doReview", "Unauthorized state crossing", async () => { 
            await wf.doReview([1],[1],[1], {from: accounts[1]})
        });

        await HlpFail.testFail("wf.doSignoff", "Unauthorized state crossing", async () => { 
            await wf.doSignoff(1, {from: accounts[1]})
        });

        await HlpFail.testFail("wf.doSignoff", "Non-existing state", async () => { 
            await wf.doSignoff(15, {from: accounts[1]})
        });

        await HlpFail.testFail("wf.doAbort", "Unauthorized state crossing", async () => { 
            await wf.doAbort(1, {from: accounts[1]})
        });

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
    });

    it('Should fail to Approve WF', async () => {

        let wf = await Workflow.deployed();

        await HlpFail.testFail("wf.doApprove", "Unauthorized state crossing", async () => { 
            await wf.doApprove(2) 
        });

        await HlpFail.testFail("wf.doApprove", "Unauthorized state crossing", async () => { 
            await wf.doApprove(4, {from: accounts[2]}) 
        });

        await HlpFail.testFail("wf.doApprove", "Non-existing state", async () => { 
            await wf.doApprove(15, {from: accounts[2]}) 
        });
    });

    it('Should fail to perform any action other than Approve', async () => {

        let wf = await Workflow.deployed();

        await HlpFail.testFail("wf.doInit", "Unauthorized state crossing", async () => { 
            await wf.doInit(1, [makeDocID(0, 1000), makeDocID(1, 101)], [0x111,0x112], {from: accounts[2]})
        });

        await HlpFail.testFail("wf.doInit", "Non-existing state", async () => { 
            await wf.doInit(15, [makeDocID(0, 1000), makeDocID(1, 101)], [0x111,0x112], {from: accounts[2]})
        });

        await HlpFail.testFail("wf.doReview", "Unauthorized state crossing", async () => { 
            await wf.doReview([1],[1],[1], {from: accounts[2]})
        });

        await HlpFail.testFail("wf.doSignoff", "Unauthorized state crossing", async () => { 
            await wf.doSignoff(1, {from: accounts[2]})
        });

        await HlpFail.testFail("wf.doSignoff", "Non-existing state", async () => { 
            await wf.doSignoff(15, {from: accounts[2]})
        });

        await HlpFail.testFail("wf.doAbort", "Unauthorized state crossing", async () => { 
            await wf.doAbort(1, {from: accounts[2]})
        });

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
    });

    it('Should fail to Review WF', async () => {

        let wf = await Workflow.deployed();

        await HlpFail.testFail("wf.doReview", "Unauthorized state crossing", async () => { 
            await wf.doReview([makeDocID(1, 101)], [makeDocID(1, 2000), makeDocID(1, 2001)], [0x111,0x112], {from: accounts[0]});
        });

        await HlpFail.testFail("wf.doReview", "No changes submitted", async () => { 
            await wf.doReview([], [], [], {from: accounts[1]});
        });

        await HlpFail.testFail("wf.doReview", "ids/content array length mismatch", async () => { 
            await wf.doReview([makeDocID(1, 101)], [makeDocID(1, 2000), makeDocID(1, 2001)], [0x111,0x112,0x113], {from: accounts[1]});
        });

        await HlpFail.testFail("wf.doReview", "Doc not found", async () => { 
            await wf.doReview([makeDocID(9, 101)], [makeDocID(1, 2000), makeDocID(1, 2001)], [0x111,0x112], {from: accounts[1]});
        });

        await HlpFail.testFail("wf.doReview", "Invalid doc type", async () => { 
            await wf.doReview([makeDocID(1, 101)], [makeDocID(9, 2000), makeDocID(1, 2001)], [0x111,0x112], {from: accounts[1]});
        });

        await HlpFail.testFail("wf.doReview", "Invalid doc hash", async () => { 
            await wf.doReview([makeDocID(1, 101)], [makeDocID(1, 2000), makeDocID(1, 2001)], [0x111,0], {from: accounts[1]});
        });

        await HlpFail.testFail("wf.doReview", "Doc type count exceeded limit", async () => { 
            await wf.doReview([makeDocID(1, 101)], [makeDocID(1, 2000), makeDocID(1, 2001), makeDocID(1, 2002)], [0x111,0x112,0x113], {from: accounts[1]});
        });

        await HlpFail.testFail("wf.doReview", "Required files missing", async () => { 
            await wf.doReview([makeDocID(0, 1000)], [], [], {from: accounts[1]});
        });

        await HlpFail.testFail("wf.doReview", "Required files missing", async () => { 
            await wf.doReview([], [makeDocID(3, 1000)], [0x1111], {from: accounts[1]});
        });

        await wf.doReview([makeDocID(1, 101)], [makeDocID(1, 2000), makeDocID(1, 2001)], [0x111,0x112], {from: accounts[1]});
        await showHistory(wf);
    });

});
