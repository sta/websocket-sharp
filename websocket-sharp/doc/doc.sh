#!/bin/sh
#
# @(#) doc.sh ver.0.0.2 2013.01.24
#
# Usage:
#   doc.sh
#
# Description:
#   Creating documentation for websocket-sharp.
#
###########################################################################

SRC_DIR="../bin/Release_Ubuntu"
XML="${SRC_DIR}/websocket-sharp.xml"
DLL="${SRC_DIR}/websocket-sharp.dll"

DOC_DIR="."
MDOC_DIR="${DOC_DIR}/mdoc"
HTML_DIR="${DOC_DIR}/html"

createDir() {
  if [ ! -d $1 ]; then
    mkdir -p $1
  fi
}

set -e
createDir ${MDOC_DIR}
createDir ${HTML_DIR}
mdoc update --delete -fno-assembly-versions -i ${XML} -o ${MDOC_DIR}/ ${DLL}
mdoc export-html -o ${HTML_DIR}/ ${MDOC_DIR}/
