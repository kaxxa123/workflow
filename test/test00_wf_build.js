const WorkflowBuilder = artifacts.require("WorkflowBuilder");
const HlpFail = require('./helpers/testFailure');
const WFRights = require('./helpers/wfhelp').WFRights;

let wf;

const existingState = async (iStartState, iEdge, iEndState) => {
    let next = await wf.states(iStartState, iEdge);
    assert(iEndState == next, "Expected state: " + iEndState + ", Found state: " + next);

    console.log(`Confirmed State Sequence Exists: [${iStartState}] -> [${iEndState}]`)
}

const missingState = async (iStartState, iEdge) => {
    await HlpFail.testFail("wf.states", "", async () => { 
        await wf.states(iStartState, iEdge);
    });
}

const existingRight = async (stateid, iEndState, user, right) => {
    let found = await wf.hasRight(stateid, iEndState, user, right)
    assert(found, `Expected state not found. [${stateid}] -> [${iEndState}] | ${user}:${right}`);

    console.log(`Confirmed Right Exists: [${stateid}] -> [${iEndState}] | ${user}:${right}`)
}

const missingRight = async (stateid, iEndState, user, right) => {
    let found = await wf.hasRight(stateid, iEndState, user, right)
    assert(!found, `Unexpected state found. [${stateid}] -> [${iEndState}] | ${user}:${right}`);

    console.log(`Confirmed Right DOES NOT Exist: [${stateid}] -> [${iEndState}] | ${user}:${right}`)
}

contract('Testing Workflow Builer', function (accounts) {

    it('Should Create Workflow OK', async () => {

        wf = await WorkflowBuilder.deployed();
        
        //Load the State Engine structure
        await wf.addState([1]);
        await wf.addState([2]);
        await wf.addState([3]);
        await wf.addState([1,2,3,4]);
        await wf.addState([]);

        //Lock the State Engine structure
        await wf.doFinalize();

        //S0
        await wf.addRight(0, 0, accounts[1], WFRights.INIT);       //S0 -> S1

        //S1
        await wf.addRight(1, 0, accounts[1], WFRights.APPROVE);    //S1 -> S2
        await wf.addRight(1, 0, accounts[2], WFRights.APPROVE);    //S1 -> S2

        //S2
        //Up to caller not to create duplicates
        await wf.addRight(2, 0, accounts[2], WFRights.APPROVE);    //S2 -> S3
        await wf.addRight(2, 0, accounts[2], WFRights.APPROVE);    //S2 -> S3

        //S3
        await wf.addRight(3, 0, accounts[1], WFRights.APPROVE);    //S3 -> S1
        await wf.addRight(3, 1, accounts[1], WFRights.APPROVE);    //S3 -> S2
        await wf.addRight(3, 2, accounts[1], WFRights.REVIEW);     //S3 -> S3
        await wf.addRight(3, 3, accounts[1], WFRights.SIGNOFF);    //S3 -> S4

        let tot = await wf.getTotalStates()
        assert(tot == 5, "Expected 5 states");

        tot = await wf.getTotalEdges(0)
        assert(tot == 1, "Expected 1 Edge");
        tot = await wf.getTotalEdges(1)
        assert(tot == 1, "Expected 1 Edge");
        tot = await wf.getTotalEdges(3)
        assert(tot == 4, "Expected 4 Edges");
        tot = await wf.getTotalEdges(4)
        assert(tot == 0, "Expected 0 Edges");

        //Verify existing states/edges
        await existingState(0,0,1)
        await existingState(1,0,2)
        await existingState(2,0,3)
        await existingState(3,0,1)
        await existingState(3,1,2)
        await existingState(3,2,3)
        await existingState(3,3,4)

        //Verify existing rights
        await existingRight(0,1, accounts[1], WFRights.INIT)
        await existingRight(1,2, accounts[1], WFRights.APPROVE)
        await existingRight(1,2, accounts[2], WFRights.APPROVE)
        await existingRight(2,3, accounts[2], WFRights.APPROVE)

        await existingRight(3,1, accounts[1], WFRights.APPROVE)
        await existingRight(3,2, accounts[1], WFRights.APPROVE)
        await existingRight(3,3, accounts[1], WFRights.REVIEW)
        await existingRight(3,4, accounts[1], WFRights.SIGNOFF)

        //Verify missing states/edges
        await missingState(0,1)
        await missingState(1,1)
        await missingState(2,1)
        await missingState(3,4)
        await missingState(4,0)
        await missingState(5,0)

        //Verify missing rights
        await missingRight(0,1, accounts[1], WFRights.APPROVE)
        await missingRight(1,2, accounts[1], WFRights.INIT)
        await missingRight(1,2, accounts[2], WFRights.REVIEW)
        await missingRight(2,3, accounts[1], WFRights.APPROVE)
        await missingRight(3,1, accounts[3], WFRights.APPROVE)
        await missingRight(3,2, accounts[3], WFRights.APPROVE)
        await missingRight(3,1, accounts[1], WFRights.REVIEW)
        await missingRight(3,4, accounts[1], WFRights.ABORT)
    });
});
