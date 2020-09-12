const WorkflowBuilder = artifacts.require("WorkflowBuilder");
const WorkflowManager = artifacts.require("WorkflowManager");
const Workflow = artifacts.require("Workflow");
const truffleAssert = require('truffle-assertions');
const HlpFail = require('./helpers/testFailure');
const wfhlp = require('./helpers/wfhelp');

const WFRights = wfhlp.WFRights;
const WFFlags = wfhlp.WFFlags;
const WFMode = wfhlp.WFMode;
const makeDocSet = wfhlp.makeDocSet;
const makeDocID = wfhlp.makeDocID;

contract('Testing Workflow Review', function (accounts) {

    var wfAddr;
    var wfID;

    it('Should Create Workflow OK', async () => {

        let engine = await WorkflowBuilder.deployed();
        let mgr = await WorkflowManager.deployed();

        let WF_ADMIN_ROLE = await mgr.WF_ADMIN_ROLE();
        await mgr.grantRole(WF_ADMIN_ROLE, accounts[0]);

        let recpt = await mgr.addWF(engine.address, 
                                [makeDocSet(1,1,WFFlags.REQUIRED),
                                makeDocSet(1,2,WFFlags.REQUIRED),
                                makeDocSet(1,2,0),
                                makeDocSet(2,2,0)]);
                                
        truffleAssert.eventEmitted(recpt, 'EventWFAdded', (ev) => {
            wfID = ev.id; 
            wfAddr = ev.addr; 
            return true; 
        });
        let wf = await Workflow.at(wfAddr);

        let engineAddr = await wf.engine()
        assert(engineAddr == engine.address, "Mismatched engine address")

        let WF_SCHEMA_ADMIN_ROLE = await engine.WF_SCHEMA_ADMIN_ROLE();
        await engine.grantRole(WF_SCHEMA_ADMIN_ROLE, accounts[0]);

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

        await wf.doInit(0,1, [makeDocID(0, 1000), makeDocID(1, 101)], [0x111,0x112], {from: accounts[1]})
        await wf.doApprove(1,2, {from: accounts[1]})
        await wf.doApprove(2,3, {from: accounts[2]})
    });

    it('Should fail to Review WF', async () => {

        let wf = await Workflow.at(wfAddr);

        //Incorrect USN
        await HlpFail.testFail("wf.doReview", "Workflow: USN mismatch", async () => { 
            await wf.doReview(2, [makeDocID(1, 101)], [makeDocID(1, 2000), makeDocID(1, 2001)], [0x111,0x112], {from: accounts[0]});
        });

        //Incorrect USN
        await HlpFail.testFail("wf.doReview", "Workflow: USN mismatch", async () => { 
            await wf.doReview(5, [makeDocID(1, 101)], [makeDocID(1, 2000), makeDocID(1, 2001)], [0x111,0x112], {from: accounts[0]});
        });

        //Sender has no Right to perform this action
        await HlpFail.testFail("wf.doReview", "Workflow: Unauthorized state crossing", async () => { 
            await wf.doReview(3, [makeDocID(1, 101)], [makeDocID(1, 2000), makeDocID(1, 2001)], [0x111,0x112], {from: accounts[0]});
        });

        //Review action is not submitting any updates
        await HlpFail.testFail("wf.doReview", "Workflow: No changes submitted", async () => { 
            await wf.doReview(3, [], [], [], {from: accounts[1]});
        });

        //idsAdd and contentAdd arrays must have a matching length
        await HlpFail.testFail("wf.doReview", "Workflow: ids/content array length mismatch", async () => { 
            await wf.doReview(3, [makeDocID(1, 101)], [makeDocID(1, 2000), makeDocID(1, 2001)], [0x111,0x112,0x113], {from: accounts[1]});
        });

        //Cannot remove non-existing id: makeDocID(9, 101)
        await HlpFail.testFail("wf.doReview", "Workflow: Doc not found", async () => { 
            await wf.doReview(3, [makeDocID(9, 101)], [makeDocID(1, 2000), makeDocID(1, 2001)], [0x111,0x112], {from: accounts[1]});
        });

        //Cannot add doc with unknown docType: makeDocID(9, 2000)
        await HlpFail.testFail("wf.doReview", "Workflow: Invalid doc type", async () => { 
            await wf.doReview(3, [makeDocID(1, 101)], [makeDocID(9, 2000), makeDocID(1, 2001)], [0x111,0x112], {from: accounts[1]});
        });

        //Cannot add/update with invalid hash
        await HlpFail.testFail("wf.doReview", "Workflow: Invalid doc hash", async () => { 
            await wf.doReview(3, [makeDocID(1, 101)], [makeDocID(1, 2000), makeDocID(1, 2001)], [0x111,0], {from: accounts[1]});
        });

        //docType=1 allows for max 2 documents
        await HlpFail.testFail("wf.doReview", "Workflow: Doc type count exceeded limit", async () => { 
            await wf.doReview(3, [makeDocID(1, 101)], [makeDocID(1, 2000), makeDocID(1, 2001), makeDocID(1, 2002)], [0x111,0x112,0x113], {from: accounts[1]});
        });

        //Removing required docType 0
        await HlpFail.testFail("wf.doReview", "Workflow: Required files missing", async () => { 
            await wf.doReview(3, [makeDocID(0, 1000)], [], [], {from: accounts[1]});
        });

        //docType 3 has a minimum of 2. Cannot add just one.
        await HlpFail.testFail("wf.doReview", "Workflow: Required files missing", async () => { 
            await wf.doReview(3, [], [makeDocID(3, 1000)], [0x1111], {from: accounts[1]});
        });
    });

    it('Should Review OK', async () => {
        let engine = await WorkflowBuilder.deployed();
        let wf = await Workflow.at(wfAddr);

        await wf.doReview(3, [makeDocID(1, 101)], [makeDocID(1, 2000), makeDocID(1, 2001)], [0x111,0x112], {from: accounts[1]});
        await wf.doReview(4, [], [makeDocID(2, 0x333)], [0x333], {from: accounts[1]});

        //Remove user right and see it fail...
        await engine.removeRight(3, 2, accounts[1], WFRights.REVIEW);       //S3 -> S3
        await HlpFail.testFail("wf.doReview", "Workflow: Unauthorized state crossing", async () => { 
            await wf.doReview(5, [], [makeDocID(3, 0x444), makeDocID(3, 0x555)], [0x444,0x555], {from: accounts[1]});
        });

        //Add different user right and see old user fail new user succeed...
        await engine.addRight(3, 2, accounts[3], WFRights.REVIEW);          //S3 -> S3
        await HlpFail.testFail("wf.doApprove", "Workflow: Unauthorized state crossing", async () => { 
            await wf.doReview(5, [], [makeDocID(3, 0x444), makeDocID(3, 0x555)], [0x444,0x555], {from: accounts[1]});
        });
        await wf.doReview(5, [], [makeDocID(3, 0x444), makeDocID(3, 0x555)], [0x444,0x555], {from: accounts[3]});

        //Add another user...
        await engine.addRight(3, 2, accounts[4], WFRights.REVIEW);          //S3 -> S3
        await wf.doReview(6, [makeDocID(2, 0x333), makeDocID(1, 2001)], [], [], {from: accounts[4]});

        state = await wf.state()
        mode = await wf.mode()
        totDocs = await wf.totalDocTypes()
        totHist = await wf.totalHistory()
        assert(state == 3, "Should be State 3");
        assert(mode == WFMode.RUNNING, "Should be RUNNING State");
        assert(totDocs == 4, "Total docs should be 4");
        assert(totHist == 7, "Total history should be 7");

        await wfhlp.showHistory(wf);
        await wfhlp.showLatest(wf);
    });
});
