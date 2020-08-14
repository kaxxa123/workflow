const WorkflowBuilder = artifacts.require("WorkflowBuilder");
const Workflow = artifacts.require("Workflow");
const HlpFail = require('./helpers/testFailure');
const WFRights = require('./helpers/wfhelp').WFRights;
const WFMode = require('./helpers/wfhelp').WFMode;
const makeDocID = require('./helpers/wfhelp').makeDocID;

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

        assert(state == 0, "Should be State 0");
        assert(mode == WFMode.UNINIT, "Should be UNINT State");
        assert(totDocs == 4, "Total docs should be 4");

        await HlpFail.testFail("wf.doInit", "Unauthorized state crossing", async () => { 
            await wf.doInit(1, [makeDocID(id=0,doctype=0), makeDocID(1, 1)], [0x111,0x112]) 
        });

        await HlpFail.testFail("wf.doInit", "ids/content array length mismatch", async () => { 
            await wf.doInit(1, [makeDocID(0, 0), makeDocID(1, 1), makeDocID(1, 2)], [0x111,0x112], {from: accounts[1]}) 
        });

        await HlpFail.testFail("wf.doInit", "Invalid doc type", async () => { 
            await wf.doInit(1, [makeDocID(1, 1), makeDocID(1, 5)], [0x111,0x112], {from: accounts[1]}) 
        });

        await HlpFail.testFail("wf.doInit", "Doc type count exceeded limit", async () => { 
            await wf.doInit(1, [makeDocID(1, 2), makeDocID(2, 2), makeDocID(3, 2)], [0x111,0x112,0x113], {from: accounts[1]}) 
        });

        await HlpFail.testFail("wf.doInit", "Initializing same ID x times", async () => { 
            await wf.doInit(1, [makeDocID(0, 0), makeDocID(1, 1), makeDocID(1, 1)], [0x111,0x112,0x113], {from: accounts[1]}) 
        });

        await HlpFail.testFail("wf.doInit", "Required files missing", async () => { 
            await wf.doInit(1, [makeDocID(0, 0), makeDocID(1, 2)], [0x111,0x112], {from: accounts[1]})
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

        await wf.doInit(1, [makeDocID(0, 0), makeDocID(1, 1)], [0x111,0x112], {from: accounts[1]})

        let state = await wf.state()
        let mode = await wf.mode()
        let totDocs = await wf.totalDocTypes()

        assert(state == 1, "Should be State 1");
        assert(mode == WFMode.RUNNING, "Should be RUNNING State");
        assert(totDocs == 4, "Total docs should be 4");
    });
});
