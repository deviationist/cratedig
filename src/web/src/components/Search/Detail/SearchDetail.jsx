import {
  addResponseChunk,
  emptyResponseBuffer,
  filterResponse,
  getResponses,
  getStatus,
  isResponseStreamComplete,
  parseFiltersFromString,
} from '../../../lib/searches';
import ErrorSegment from '../../Shared/ErrorSegment';
import LoaderSegment from '../../Shared/LoaderSegment';
import Switch from '../../Shared/Switch';
import Response from '../Response';
import SearchDetailHeader from './SearchDetailHeader';
import React, { useEffect, useMemo, useRef, useState } from 'react';
import {
  Button,
  Checkbox,
  Dropdown,
  Icon,
  Input,
  Segment,
} from 'semantic-ui-react';

const sortDropdownOptions = [
  {
    key: 'uploadSpeed',
    text: 'Upload Speed (Fastest to Slowest)',
    value: 'uploadSpeed',
  },
  {
    key: 'queueLength',
    text: 'Queue Depth (Least to Most)',
    value: 'queueLength',
  },
];

// how long a gap in the streamed responses may stay before subscribing again
const GAP_WAIT_MS = 2_000;
// how long the server may say nothing about a running search before asking
// whether it ended
const QUIET_MS = 20_000;
// how often to look for either of those
const WATCHDOG_MS = 1_000;
// how long to wait for the closing chunk after the search's update says it
// ended, before fetching the responses instead
const CLOSE_WAIT_MS = 3_000;

// the line shown while a search runs
const describeProgress = ({
  fileCount,
  lockedFileCount,
  responseCount,
  state,
}) =>
  state === 'InProgress'
    ? `Found ${fileCount} files ${
        lockedFileCount > 0 ? `(plus ${lockedFileCount} locked) ` : ''
      }from ${responseCount} users`
    : 'Loading results...';

const SearchDetail = ({
  creating,
  disabled,
  hub,
  onCreate,
  onRemove,
  onSearchChanged,
  onStop,
  removing,
  search,
  stopping,
}) => {
  const { id, isComplete } = search;

  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(undefined);

  const [results, setResults] = useState([]);

  // filters and sorting options
  const [hiddenResults, setHiddenResults] = useState([]);
  const [resultSort, setResultSort] = useState('uploadSpeed');
  const [hideLocked, setHideLocked] = useState(true);
  const [hideNoFreeSlots, setHideNoFreeSlots] = useState(false);
  const [foldResults, setFoldResults] = useState(false);
  const [resultFilters, setResultFilters] = useState('');
  const [displayCount, setDisplayCount] = useState(5);

  // A search in progress streams its responses: the page subscribes over the
  // hub, which answers with those received so far, then sends the rest in
  // numbered chunks and closes with a chunk naming the total, once they are
  // saved. Holding the total means holding them all, so nothing is fetched
  // again. Each way this can go wrong has a fallback:
  //
  // - a chunk lost on the way leaves a gap the later ones wait behind; if it
  //   is still there after GAP_WAIT_MS, subscribe again for a fresh snapshot
  // - the search's own update saying it ended can be lost, and the closing
  //   chunk with it; after QUIET_MS without a word, ask for its state
  // - the closing chunk can be lost alone; the search's update still arrives,
  //   and after CLOSE_WAIT_MS the responses are fetched in full
  const streamRef = useRef(emptyResponseBuffer);
  const streamingRef = useRef(false);
  const lastHeardRef = useRef(Date.now());
  const [streamComplete, setStreamComplete] = useState(false);

  // every update to the search counts as hearing from the server
  useEffect(() => {
    lastHeardRef.current = Date.now();
  }, [search]);

  // ask the server for the search's state, and take it if it has ended: for
  // when the update saying so went missing
  const checkEnded = async () => {
    try {
      const status = await getStatus({ id });

      if (status?.isComplete) {
        onSearchChanged?.(status);
      }
    } catch (statusError) {
      console.warn('could not check whether the search ended', statusError);
    }
  };

  const checkEndedRef = useRef(checkEnded);
  checkEndedRef.current = checkEnded;

  useEffect(() => {
    // a search that had already ended when the page opened has nothing to
    // stream; its responses are fetched below
    if (!hub || search.isComplete) {
      return undefined;
    }

    let active = true;
    // when the oldest chunk still waiting for a gap to fill arrived
    let gapSince;
    streamRef.current = emptyResponseBuffer;
    streamingRef.current = true;
    setStreamComplete(false);

    const apply = (chunk) => {
      if (!active || !chunk || chunk.searchId !== id) return;

      lastHeardRef.current = Date.now();
      streamRef.current = addResponseChunk(streamRef.current, chunk);
      setResults(streamRef.current.responses);

      if (streamRef.current.pending.length === 0) {
        gapSince = undefined;
      } else {
        gapSince ??= Date.now();
      }

      if (isResponseStreamComplete(streamRef.current)) {
        setStreamComplete(true);
      }
    };

    const subscribe = async () => {
      try {
        const snapshot = await hub.invoke('Subscribe', id);

        if (!active) return;

        // the search is no longer running: its responses are saved, and
        // its update saying so is on the way, or was lost
        if (!snapshot) {
          streamingRef.current = false;
          checkEndedRef.current();
          return;
        }

        apply(snapshot);
      } catch (subscribeError) {
        // not fatal: the responses are fetched in full once it ends
        console.warn('could not subscribe to search responses', subscribeError);
        streamingRef.current = false;
      }
    };

    const watchdog = setInterval(() => {
      const now = Date.now();

      if (gapSince !== undefined && now - gapSince > GAP_WAIT_MS) {
        gapSince = undefined;
        subscribe();
      }

      if (now - lastHeardRef.current > QUIET_MS) {
        lastHeardRef.current = now;
        checkEndedRef.current();
      }
    }, WATCHDOG_MS);

    const unsubscribe = async () => {
      try {
        await hub.invoke('Unsubscribe', id);
      } catch {
        // the connection is gone, and the subscription with it
      }
    };

    hub.on('responses', apply);
    subscribe();

    return () => {
      active = false;
      streamingRef.current = false;
      clearInterval(watchdog);
      hub.off('responses', apply);
      unsubscribe();
    };
    // started once per search; it follows the search's state through refs
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [hub, id]);

  // the closing chunk says the search ended even when its update went missing
  useEffect(() => {
    if (streamComplete && !isComplete) {
      checkEndedRef.current();
    }
  }, [isComplete, streamComplete]);

  // once the search has ended: nothing to do if the stream delivered every
  // response. Otherwise fetch them -- without a loader when some streamed in,
  // and after giving the closing chunk CLOSE_WAIT_MS to arrive, since the
  // search's update usually overtakes it
  useEffect(() => {
    if (!isComplete || streamComplete) {
      return undefined;
    }

    let active = true;
    const streamed = streamRef.current.responses.length > 0;

    const get = async () => {
      try {
        if (!streamed) setLoading(true);

        const responses = await getResponses({ id });

        if (active) {
          setResults(responses);
          setLoading(false);
        }
      } catch (getError) {
        if (active) {
          setError(getError);
          setLoading(false);
        }
      }
    };

    const timer = setTimeout(get, streamingRef.current ? CLOSE_WAIT_MS : 0);

    return () => {
      active = false;
      clearTimeout(timer);
    };
  }, [id, isComplete, streamComplete]);

  // apply sorting and filters.  this can take a while for larger result
  // sets, so memoize it.
  const sortedAndFilteredResults = useMemo(() => {
    const sortOptions = {
      queueLength: { field: 'queueLength', order: 'asc' },
      uploadSpeed: { field: 'uploadSpeed', order: 'desc' },
    };

    const { field, order } = sortOptions[resultSort];

    const filters = parseFiltersFromString(resultFilters);

    return results
      .filter((r) => !hiddenResults.includes(r.username))
      .map((r) => {
        if (hideLocked) {
          return { ...r, lockedFileCount: 0, lockedFiles: [] };
        }

        return r;
      })
      .map((response) => filterResponse({ filters, response }))
      .filter((r) => r.fileCount + r.lockedFileCount > 0)
      .filter((r) => !(hideNoFreeSlots && !r.hasFreeUploadSlot))
      .sort((a, b) => {
        if (order === 'asc') {
          return a[field] - b[field];
        }

        return b[field] - a[field];
      });
  }, [
    hiddenResults,
    hideLocked,
    hideNoFreeSlots,
    resultFilters,
    resultSort,
    results,
  ]);

  // when a user uses the action buttons, we will *probably* re-use this component,
  // but with a new search ID.  clear everything to prepare for the transition
  const reset = () => {
    setLoading(false);
    setError(undefined);
    setResults([]);
    streamRef.current = emptyResponseBuffer;
    setHiddenResults([]);
    setDisplayCount(5);
  };

  const create = async ({ navigate, search: searchForCreate }) => {
    reset();
    onCreate({ navigate, search: searchForCreate });
  };

  const remove = async () => {
    reset();
    onRemove(search);
  };

  const progress = describeProgress(search);

  const filteredCount = results?.length - sortedAndFilteredResults.length;
  const remainingCount = sortedAndFilteredResults.length - displayCount;
  const loaded = !removing && !creating && !loading && results;

  if (error) {
    return <ErrorSegment caption={error?.message ?? error} />;
  }

  return (
    <>
      <SearchDetailHeader
        creating={creating}
        disabled={disabled}
        loaded={loaded}
        loading={loading}
        onCreate={create}
        onRemove={remove}
        onStop={onStop}
        removing={removing}
        search={search}
        stopping={stopping}
      />
      <Switch
        loading={loading && <LoaderSegment />}
        searching={
          !isComplete &&
          results.length === 0 && <LoaderSegment>{progress}</LoaderSegment>
        }
      >
        {!isComplete && (
          <Segment
            className="search-progress"
            raised
          >
            <Icon
              loading
              name="circle notch"
            />
            {progress}
          </Segment>
        )}
        {loaded && (
          <Segment
            className="search-options"
            raised
          >
            <Dropdown
              button
              className="search-options-sort icon"
              floating
              icon="sort"
              labeled
              onChange={(_event, { value }) => setResultSort(value)}
              options={sortDropdownOptions}
              text={
                sortDropdownOptions.find((o) => o.value === resultSort).text
              }
            />
            <div className="search-option-toggles">
              <Checkbox
                checked={hideLocked}
                className="search-options-hide-locked"
                label="Hide Locked Results"
                onChange={() => setHideLocked(!hideLocked)}
                toggle
              />
              <Checkbox
                checked={hideNoFreeSlots}
                className="search-options-hide-no-slots"
                label="Hide Results with No Free Slots"
                onChange={() => setHideNoFreeSlots(!hideNoFreeSlots)}
                toggle
              />
              <Checkbox
                checked={foldResults}
                className="search-options-fold-results"
                label="Fold Results"
                onChange={() => setFoldResults(!foldResults)}
                toggle
              />
            </div>
            <Input
              action={
                Boolean(resultFilters) && {
                  color: 'red',
                  icon: 'x',
                  onClick: () => setResultFilters(''),
                }
              }
              className="search-filter"
              label={{ content: 'Filter', icon: 'filter' }}
              onChange={(_event, data) => setResultFilters(data.value)}
              placeholder="
                lackluster container -bothersome iscbr|isvbr islossless|islossy 
                minbitrate:320 minbitdepth:24 minfilesize:10 minfilesinfolder:8 minlength:5000
              "
              value={resultFilters}
            />
          </Segment>
        )}
        {loaded &&
          sortedAndFilteredResults.slice(0, displayCount).map((r) => (
            <Response
              disabled={disabled}
              isInitiallyFolded={foldResults}
              key={r.username}
              onHide={() => setHiddenResults([...hiddenResults, r.username])}
              response={r}
            />
          ))}
        {loaded &&
          (remainingCount > 0 ? (
            <Button
              className="showmore-button"
              fluid
              onClick={() => setDisplayCount(displayCount + 5)}
              primary
              size="large"
            >
              Show {remainingCount > 5 ? 5 : remainingCount} More Results{' '}
              {`(${remainingCount} remaining, ${filteredCount} hidden by filter(s))`}
            </Button>
          ) : filteredCount > 0 ? (
            <Button
              className="showmore-button"
              disabled
              fluid
              size="large"
            >{`All results shown. ${filteredCount} results hidden by filter(s)`}</Button>
          ) : (
            ''
          ))}
      </Switch>
    </>
  );
};

export default SearchDetail;
