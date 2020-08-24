const BigNumber = require('bignumber.js');

const WFRights = {
    INIT:    0,    // Start Workflow by submitting 1st document set version
    APPROVE: 1,    // Approve the current document set version
    REVIEW:  2,    // Submit an updated document set
    SIGNOFF: 3,    // Conclude workflow successfully
    ABORT:   4     // Abort workflow
}

const WFMode = {
    UNINIT:   0,    //State Engine Unintialized
    RUNNING:  1,    //State Engine Running
    COMPLETE: 2,    //State Engine Ended Success
    ABORTED:  3     //State Entine Ended Aborted
}

const WFFlags = {
    REQUIRED: 1,    //Document is required
    PUBLIC:   2     //Document is to be published
}

function myPad(number, length) {
    var str = '' + number;
    while (str.length < length) {
        str = '0' + str;
    }
    return str;
}

function makeDocID(doctype, id) { 
    let str = "0x" + myPad(id.toString(16), 8)+ myPad(doctype.toString(16), 8);
    return BigNumber(str); 
}

function makeDocSet(loLimit, hiLimit, flags) {
    let str = "0x" + myPad(flags.toString(16), 8) + myPad(hiLimit.toString(16), 8) + myPad(loLimit.toString(16), 8);
    return BigNumber(str); 
}

async function getLatest(wf) {
    //Discover the set of current documents by tranversing the history
    let totHist = await wf.totalHistory()
    let docIds = [];

    for (cnt = 0; cnt <totHist; ++cnt) {
        let history = await wf.getHistory(cnt);

        if ((history.action == WFRights.INIT)  && (history.idsAdd)) {
            history.idsAdd.forEach( item => docIds.push(BigNumber(item)));
        }

        else if (history.action == WFRights.REVIEW) {

            if (history.idsRmv) {
                history.idsRmv.forEach( item => {
                    let idx = docIds.findIndex(item2 => item2.isEqualTo(item));
                    assert(idx >= 0, "Unexpected: Index Not Found, on traversing history!")
                    docIds.splice(idx,1);
                });
            }

            if (history.idsAdd) {
                history.idsAdd.forEach( item => {
                    let idx = docIds.findIndex(item2 => item2.isEqualTo(item));
                    if (idx == -1) {
                        docIds.push(BigNumber(item));
                    }
                });
            }
        }
    }

    return docIds;
}

module.exports = {
    WFRights,
    WFMode,
    WFFlags,
    makeDocID,
    makeDocSet,
    getLatest
}
