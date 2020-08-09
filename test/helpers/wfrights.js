const WFRights = {
    INIT:    0,    // Start Workflow by submitting 1st document set version
    APPROVE: 1,    // Approve the current document set version
    REVIEW:  2,    // Submit an updated document set
    SIGNOFF: 3,    // Conclude workflow successfully
    ABORT:   4     // Abort workflow
}

module.exports = {
    WFRights
}
