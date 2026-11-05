import React, {useEffect, useRef, useState} from 'react';
import {View, Text, Pressable, ScrollView, TextInput, useWindowDimensions} from 'react-native';
import UnityView from '@azesmway/react-native-unity';

export default function App() {
  const unityRef = useRef<UnityView>(null);
  const messageCounter = useRef(0);
  const [last, setLast] = useState('nothing yet');
  const [showAR, setShowAR] = useState(true);
  const [showControls, setShowControls] = useState(false);
  const [loopValues, setLoopValues] = useState('5,10,15');
  const [selectionValue, setSelectionValue] = useState('75');
  const [selectionCondition, setSelectionCondition] = useState('>= 50');
  const [ladderScore, setLadderScore] = useState('75');
  const [nestedValues, setNestedValues] = useState('65,80');
  const [memoryVariableInput, setMemoryVariableInput] = useState('int age=20');
  const [activeTopicId, setActiveTopicId] = useState('');
  const [activeActivityId, setActiveActivityId] = useState('');
  const {width, height} = useWindowDimensions();

  const handleUnityMessage = (event: {nativeEvent: {message: string}}) => {
    const message = event.nativeEvent.message;
    setLast(message);
    try {
      const parsed = JSON.parse(message);
      if (parsed.type === 'topic.loaded') {
        setActiveTopicId(parsed.payload?.topicId ?? '');
      }
    } catch {
      // Keep displaying non-JSON Unity messages in the temporary test panel.
    }
  };

  // Temporary contract controls for testing the modular AR session on-device.
  const sendToUnity = (type: string, payload: object = {}) => {
    if (type === 'topic.open') {
      const topicRequest = payload as {topicId?: string; activityId?: string};
      setActiveActivityId(topicRequest.activityId ?? '');
    } else if (type === 'topic.close' || type === 'placement.reset' || type === 'session.stop') {
      setActiveActivityId('');
    }

    messageCounter.current += 1;
    const message = {
      v: 1,
      id: `s-${Date.now()}-${messageCounter.current}`,
      type,
      payload,
    };
    unityRef.current?.postMessage(
      'LociBridge',
      'OnShellMessage',
      JSON.stringify(message),
    );
  };

  // Pause Unity when its screen is covered, resume when it comes back.
  useEffect(() => {
    if (showAR) {
      unityRef.current?.resumeUnity();
      unityRef.current?.windowFocusChanged(true);
    } else {
      unityRef.current?.pauseUnity(true);
    }
  }, [showAR]);

  const btn = {marginTop: 8, padding: 14, backgroundColor: '#5B4BFF'};
  const btnText = {color: '#fff', fontWeight: '600' as const};
  const landscapeGuidanceTopic = activeTopicId === 'list'
    ? 'List'
    : activeTopicId === 'queue'
      ? 'Queue'
      : activeTopicId === 'linked_list'
        ? 'Linked List'
        : '';

  return (
    <View style={{flex: 1, backgroundColor: '#F4F5F9'}}>
      <UnityView
        ref={unityRef}
        androidKeepPlayerMounted={true}
        style={{flex: 1}}
        onUnityMessage={handleUnityMessage}
      />
      {activeTopicId === 'loops' && height > width && (
        <View style={{
          position: 'absolute', top: 96, left: 16, right: 16, zIndex: 1,
          paddingHorizontal: 14, paddingVertical: 10, borderRadius: 12,
          backgroundColor: 'rgba(18,20,43,0.88)',
        }}>
          <Text style={{color: '#fff', fontWeight: '600', textAlign: 'center'}}>
            For a clearer view of Loops, rotate your phone to landscape.
          </Text>
        </View>
      )}
      {landscapeGuidanceTopic !== '' && height > width && (
        <View style={{
          position: 'absolute', top: 96, left: 16, right: 16, zIndex: 1,
          paddingHorizontal: 14, paddingVertical: 10, borderRadius: 12,
          backgroundColor: 'rgba(18,20,43,0.88)',
        }}>
          <Text style={{color: '#fff', fontWeight: '600', textAlign: 'center'}}>
            For a clearer view of {landscapeGuidanceTopic}, rotate your phone to landscape.
          </Text>
        </View>
      )}
      {activeTopicId === 'memory' && (activeActivityId === 'variables' || activeActivityId === 'data_types' || activeActivityId === 'address_layout' || activeActivityId === 'memory_access' || activeActivityId === 'loop_memory') && height > width && (
        <View style={{
          position: 'absolute', top: 96, left: 16, right: 16, zIndex: 1,
          paddingHorizontal: 14, paddingVertical: 10, borderRadius: 12,
          backgroundColor: 'rgba(18,20,43,0.88)',
        }}>
          <Text style={{color: '#fff', fontWeight: '600', textAlign: 'center'}}>
            For a clearer view of {activeActivityId === 'data_types' ? 'Memory Data Types' : activeActivityId === 'address_layout' ? 'Memory Address Layout' : activeActivityId === 'memory_access' ? 'Memory Access' : activeActivityId === 'loop_memory' ? 'Loop Memory' : 'Memory Variables'}, rotate your phone to landscape.
          </Text>
        </View>
      )}
      {!showAR && (
        <View
          style={{
            position: 'absolute', top: 0, left: 0, right: 0, bottom: 0,
            backgroundColor: '#F4F5F9', alignItems: 'center', justifyContent: 'center',
          }}>
          <Text style={{fontSize: 22}}>Another screen (AR paused)</Text>
        </View>
      )}
      <Pressable
        onPress={() => setShowControls(visible => !visible)}
        style={{
          position: 'absolute', top: 48, right: 16, zIndex: 2,
          paddingHorizontal: 14, paddingVertical: 10,
          borderRadius: 16, backgroundColor: 'rgba(18,20,43,0.82)',
        }}>
        <Text style={{color: '#fff', fontWeight: '600'}}>
          {showControls ? 'Hide test controls' : 'Test controls'}
        </Text>
      </Pressable>
      {showControls && (
        <View style={{
          position: 'absolute', top: 94, left: 16, right: 16, zIndex: 1,
          maxHeight: '55%', padding: 12, borderRadius: 16,
          backgroundColor: 'rgba(18,20,43,0.92)',
        }}>
          <ScrollView>
            <Text style={{color: '#fff', padding: 8}} numberOfLines={3}>
              From Unity: {last}
            </Text>
            <Text style={{color: '#fff', padding: 8, fontWeight: '600'}}>
              Temporary AR test controls
            </Text>
            <Pressable
              onPress={() => sendToUnity('diag.ping', {nonce: `android-${Date.now()}`})}
              style={btn}>
              <Text style={btnText}>Send diag.ping</Text>
            </Pressable>
            <Pressable onPress={() => sendToUnity('session.start')} style={btn}>
              <Text style={btnText}>session.start</Text>
            </Pressable>
            <Pressable onPress={() => sendToUnity('session.stop')} style={btn}>
              <Text style={btnText}>session.stop</Text>
            </Pressable>
            <Pressable onPress={() => sendToUnity('placement.confirm')} style={btn}>
              <Text style={btnText}>placement.confirm (Place here)</Text>
            </Pressable>
            <Pressable onPress={() => sendToUnity('placement.reset')} style={btn}>
              <Text style={btnText}>placement.reset</Text>
            </Pressable>
            <Pressable
              onPress={() => sendToUnity('topic.open', {topicId: 'list'})}
              style={btn}>
              <Text style={btnText}>Open List</Text>
            </Pressable>
            <Pressable
              onPress={() =>
                sendToUnity('topic.action', {
                  topicId: 'list',
                  action: 'Insert',
                  value: '40',
                })
              }
              style={btn}>
              <Text style={btnText}>Insert 40</Text>
            </Pressable>
            <Pressable
              onPress={() =>
                sendToUnity('topic.action', {
                  topicId: 'list',
                  action: 'Delete',
                })
              }
              style={btn}>
              <Text style={btnText}>Delete last value</Text>
            </Pressable>
            <Pressable
              onPress={() =>
                sendToUnity('topic.action', {
                  topicId: 'list',
                  action: 'Search',
                  value: '20',
                })
              }
              style={btn}>
              <Text style={btnText}>Search 20</Text>
            </Pressable>
            <Pressable
              onPress={() =>
                sendToUnity('topic.action', {
                  topicId: 'list',
                  action: 'Traverse',
                })
              }
              style={btn}>
              <Text style={btnText}>Traverse list</Text>
            </Pressable>
            <Pressable
              onPress={() =>
                sendToUnity('topic.action', {
                  topicId: 'list',
                  action: 'Reset',
                })
              }
              style={btn}>
              <Text style={btnText}>Reset list</Text>
            </Pressable>
            <Pressable onPress={() => sendToUnity('topic.open', {topicId: 'stack'})} style={btn}>
              <Text style={btnText}>Open Stack</Text>
            </Pressable>
            <Pressable onPress={() => sendToUnity('topic.action', {topicId: 'stack', action: 'Push', value: '40'})} style={btn}>
              <Text style={btnText}>Push 40</Text>
            </Pressable>
            <Pressable onPress={() => sendToUnity('topic.action', {topicId: 'stack', action: 'Pop'})} style={btn}>
              <Text style={btnText}>Pop</Text>
            </Pressable>
            <Pressable onPress={() => sendToUnity('topic.action', {topicId: 'stack', action: 'Peek'})} style={btn}>
              <Text style={btnText}>Peek</Text>
            </Pressable>
            <Pressable onPress={() => sendToUnity('topic.action', {topicId: 'stack', action: 'Search', value: '20'})} style={btn}>
              <Text style={btnText}>Search 20 in Stack</Text>
            </Pressable>
            <Pressable onPress={() => sendToUnity('topic.action', {topicId: 'stack', action: 'Traverse'})} style={btn}>
              <Text style={btnText}>Traverse Stack</Text>
            </Pressable>
            <Pressable onPress={() => sendToUnity('topic.action', {topicId: 'stack', action: 'Reset'})} style={btn}>
              <Text style={btnText}>Reset Stack</Text>
            </Pressable>
            <Pressable onPress={() => sendToUnity('topic.open', {topicId: 'queue'})} style={btn}>
              <Text style={btnText}>Open Queue</Text>
            </Pressable>
            <Pressable onPress={() => sendToUnity('topic.action', {topicId: 'queue', action: 'Enqueue', value: '40'})} style={btn}>
              <Text style={btnText}>Enqueue 40</Text>
            </Pressable>
            <Pressable onPress={() => sendToUnity('topic.action', {topicId: 'queue', action: 'Dequeue'})} style={btn}>
              <Text style={btnText}>Dequeue</Text>
            </Pressable>
            <Pressable onPress={() => sendToUnity('topic.action', {topicId: 'queue', action: 'Peek'})} style={btn}>
              <Text style={btnText}>Peek Queue</Text>
            </Pressable>
            <Pressable onPress={() => sendToUnity('topic.action', {topicId: 'queue', action: 'Search', value: '20'})} style={btn}>
              <Text style={btnText}>Search 20 in Queue</Text>
            </Pressable>
            <Pressable onPress={() => sendToUnity('topic.action', {topicId: 'queue', action: 'Traverse'})} style={btn}>
              <Text style={btnText}>Traverse Queue</Text>
            </Pressable>
            <Pressable onPress={() => sendToUnity('topic.action', {topicId: 'queue', action: 'Reset'})} style={btn}>
              <Text style={btnText}>Reset Queue</Text>
            </Pressable>
            <Pressable onPress={() => sendToUnity('topic.open', {topicId: 'linked_list'})} style={btn}>
              <Text style={btnText}>Open Linked List</Text>
            </Pressable>
            <Pressable onPress={() => sendToUnity('topic.action', {topicId: 'linked_list', action: 'Insert', value: '40'})} style={btn}>
              <Text style={btnText}>Insert 40 at tail</Text>
            </Pressable>
            <Pressable onPress={() => sendToUnity('topic.action', {topicId: 'linked_list', action: 'Delete Head'})} style={btn}>
              <Text style={btnText}>Delete Head</Text>
            </Pressable>
            <Pressable onPress={() => sendToUnity('topic.action', {topicId: 'linked_list', action: 'Search', value: '20'})} style={btn}>
              <Text style={btnText}>Search 20 in Linked List</Text>
            </Pressable>
            <Pressable onPress={() => sendToUnity('topic.action', {topicId: 'linked_list', action: 'Traverse'})} style={btn}>
              <Text style={btnText}>Traverse Linked List</Text>
            </Pressable>
            <Pressable onPress={() => sendToUnity('topic.action', {topicId: 'linked_list', action: 'Reset'})} style={btn}>
              <Text style={btnText}>Reset Linked List</Text>
            </Pressable>
            <Pressable onPress={() => sendToUnity('topic.open', {topicId: 'functions'})} style={btn}>
              <Text style={btnText}>Open Functions</Text>
            </Pressable>
            <Pressable onPress={() => sendToUnity('topic.action', {topicId: 'functions', action: 'Set Input', value: '15,25'})} style={btn}>
              <Text style={btnText}>Set Function Inputs: 15, 25</Text>
            </Pressable>
            <Pressable onPress={() => sendToUnity('topic.action', {topicId: 'functions', action: 'Add'})} style={btn}>
              <Text style={btnText}>Call Add</Text>
            </Pressable>
            <Pressable onPress={() => sendToUnity('topic.action', {topicId: 'functions', action: 'Subtract'})} style={btn}>
              <Text style={btnText}>Call Subtract</Text>
            </Pressable>
            <Pressable onPress={() => sendToUnity('topic.action', {topicId: 'functions', action: 'Multiply'})} style={btn}>
              <Text style={btnText}>Call Multiply</Text>
            </Pressable>
            <Pressable onPress={() => sendToUnity('topic.action', {topicId: 'functions', action: 'Reset'})} style={btn}>
              <Text style={btnText}>Reset Functions</Text>
            </Pressable>
            <Pressable onPress={() => sendToUnity('topic.open', {topicId: 'selection', activityId: 'basic_if'})} style={btn}>
              <Text style={btnText}>Open Basic If</Text>
            </Pressable>
            <TextInput
              value={selectionValue}
              onChangeText={setSelectionValue}
              placeholder="Enter a whole number, e.g. 75"
              keyboardType="numbers-and-punctuation"
              style={{marginTop: 8, padding: 14, backgroundColor: '#fff', color: '#12142B'}}
            />
            <Pressable onPress={() => sendToUnity('topic.action', {topicId: 'selection', action: 'Set Value', value: selectionValue})} style={btn}>
              <Text style={btnText}>Set selection value</Text>
            </Pressable>
            <TextInput
              value={selectionCondition}
              onChangeText={setSelectionCondition}
              placeholder="Condition, e.g. >= 50"
              style={{marginTop: 8, padding: 14, backgroundColor: '#fff', color: '#12142B'}}
            />
            <Pressable onPress={() => sendToUnity('topic.action', {topicId: 'selection', action: 'Set Condition', value: selectionCondition})} style={btn}>
              <Text style={btnText}>Set selection condition</Text>
            </Pressable>
            <Pressable onPress={() => sendToUnity('topic.action', {topicId: 'selection', action: 'Check'})} style={btn}>
              <Text style={btnText}>Check condition</Text>
            </Pressable>
            <Pressable onPress={() => sendToUnity('topic.action', {topicId: 'selection', action: 'Toggle Rule'})} style={btn}>
              <Text style={btnText}>Toggle Basic If rule</Text>
            </Pressable>
            <Pressable onPress={() => sendToUnity('topic.action', {topicId: 'selection', action: 'Reset'})} style={btn}>
              <Text style={btnText}>Reset Basic If</Text>
            </Pressable>
            <Pressable onPress={() => sendToUnity('topic.open', {topicId: 'selection', activityId: 'else_if_ladder'})} style={btn}>
              <Text style={btnText}>Open Else-if Ladder</Text>
            </Pressable>
            <TextInput
              value={ladderScore}
              onChangeText={setLadderScore}
              placeholder="Enter a whole-number score, e.g. 75"
              keyboardType="numbers-and-punctuation"
              style={{marginTop: 8, padding: 14, backgroundColor: '#fff', color: '#12142B'}}
            />
            <Pressable onPress={() => sendToUnity('topic.action', {topicId: 'selection', action: 'Set Score', value: ladderScore})} style={btn}>
              <Text style={btnText}>Set ladder score</Text>
            </Pressable>
            <Pressable onPress={() => sendToUnity('topic.action', {topicId: 'selection', action: 'Rule Info'})} style={btn}>
              <Text style={btnText}>Ladder rule info</Text>
            </Pressable>
            <Pressable onPress={() => sendToUnity('topic.action', {topicId: 'selection', action: 'Check'})} style={btn}>
              <Text style={btnText}>Check ladder</Text>
            </Pressable>
            <Pressable onPress={() => sendToUnity('topic.action', {topicId: 'selection', action: 'Toggle Rule'})} style={btn}>
              <Text style={btnText}>Toggle ladder rule</Text>
            </Pressable>
            <Pressable onPress={() => sendToUnity('topic.action', {topicId: 'selection', action: 'Reset'})} style={btn}>
              <Text style={btnText}>Reset Else-if Ladder</Text>
            </Pressable>
            <Pressable onPress={() => sendToUnity('topic.open', {topicId: 'selection', activityId: 'nested_if'})} style={btn}>
              <Text style={btnText}>Open Nested If</Text>
            </Pressable>
            <TextInput
              value={nestedValues}
              onChangeText={setNestedValues}
              placeholder="Enter two whole numbers, e.g. 65,80"
              keyboardType="numbers-and-punctuation"
              style={{marginTop: 8, padding: 14, backgroundColor: '#fff', color: '#12142B'}}
            />
            <Pressable onPress={() => sendToUnity('topic.action', {topicId: 'selection', action: 'Set Values', value: nestedValues})} style={btn}>
              <Text style={btnText}>Set nested values</Text>
            </Pressable>
            <Pressable onPress={() => sendToUnity('topic.action', {topicId: 'selection', action: 'Rule Info'})} style={btn}>
              <Text style={btnText}>Nested rule info</Text>
            </Pressable>
            <Pressable onPress={() => sendToUnity('topic.action', {topicId: 'selection', action: 'Check'})} style={btn}>
              <Text style={btnText}>Check nested conditions</Text>
            </Pressable>
            <Pressable onPress={() => sendToUnity('topic.action', {topicId: 'selection', action: 'Toggle Rule'})} style={btn}>
              <Text style={btnText}>Toggle nested rule</Text>
            </Pressable>
            <Pressable onPress={() => sendToUnity('topic.action', {topicId: 'selection', action: 'Reset'})} style={btn}>
              <Text style={btnText}>Reset Nested If</Text>
            </Pressable>
            <Pressable onPress={() => sendToUnity('topic.open', {topicId: 'memory', activityId: 'overview'})} style={btn}>
              <Text style={btnText}>Open Memory Overview</Text>
            </Pressable>
            <Pressable onPress={() => sendToUnity('topic.open', {topicId: 'memory', activityId: 'variables'})} style={btn}>
              <Text style={btnText}>Open Memory Variables</Text>
            </Pressable>
            <Pressable onPress={() => sendToUnity('topic.open', {topicId: 'memory', activityId: 'data_types'})} style={btn}>
              <Text style={btnText}>Open Memory Data Types</Text>
            </Pressable>
            <Pressable onPress={() => sendToUnity('topic.open', {topicId: 'memory', activityId: 'address_layout'})} style={btn}>
              <Text style={btnText}>Open Memory Address Layout</Text>
            </Pressable>
            <Pressable onPress={() => sendToUnity('topic.open', {topicId: 'memory', activityId: 'memory_access'})} style={btn}>
              <Text style={btnText}>Open Memory Access</Text>
            </Pressable>
            <Pressable onPress={() => sendToUnity('topic.open', {topicId: 'memory', activityId: 'loop_memory'})} style={btn}>
              <Text style={btnText}>Open Loop Memory</Text>
            </Pressable>
            {activeActivityId === 'data_types' && (
              <>
                <Pressable onPress={() => sendToUnity('topic.action', {topicId: 'memory', action: 'Show Types'})} style={btn}>
                  <Text style={btnText}>Show Data Types</Text>
                </Pressable>
                <Pressable onPress={() => sendToUnity('topic.action', {topicId: 'memory', action: 'Reset'})} style={btn}>
                  <Text style={btnText}>Reset Data Types</Text>
                </Pressable>
              </>
            )}
            {activeActivityId === 'address_layout' && (
              <>
                <Pressable onPress={() => sendToUnity('topic.action', {topicId: 'memory', action: 'Show Layout'})} style={btn}>
                  <Text style={btnText}>Show Address Layout</Text>
                </Pressable>
                <Pressable onPress={() => sendToUnity('topic.action', {topicId: 'memory', action: 'Reset'})} style={btn}>
                  <Text style={btnText}>Reset Address Layout</Text>
                </Pressable>
              </>
            )}
            {activeActivityId === 'memory_access' && (
              <>
                <Pressable onPress={() => sendToUnity('topic.action', {topicId: 'memory', action: 'Access Step'})} style={btn}>
                  <Text style={btnText}>Access Step</Text>
                </Pressable>
                <Pressable onPress={() => sendToUnity('topic.action', {topicId: 'memory', action: 'Reset'})} style={btn}>
                  <Text style={btnText}>Reset Memory Access</Text>
                </Pressable>
              </>
            )}
            {activeActivityId === 'loop_memory' && (
              <>
                <Pressable onPress={() => sendToUnity('topic.action', {topicId: 'memory', action: 'Loop Step'})} style={btn}>
                  <Text style={btnText}>Loop Step</Text>
                </Pressable>
                <Pressable onPress={() => sendToUnity('topic.action', {topicId: 'memory', action: 'Reset'})} style={btn}>
                  <Text style={btnText}>Reset Loop Memory</Text>
                </Pressable>
              </>
            )}
            {activeActivityId === 'variables' && (
              <>
                <TextInput
                  value={memoryVariableInput}
                  onChangeText={setMemoryVariableInput}
                  placeholder="int age=20, age=25, age, or prediction"
                  autoCapitalize="none"
                  style={{marginTop: 8, padding: 14, backgroundColor: '#fff', color: '#12142B'}}
                />
                <Pressable onPress={() => sendToUnity('topic.action', {topicId: 'memory', action: 'Set/Inspect', value: memoryVariableInput})} style={btn}>
                  <Text style={btnText}>Set / Inspect Variable</Text>
                </Pressable>
                <Pressable onPress={() => sendToUnity('topic.action', {topicId: 'memory', action: 'Predict Step', value: memoryVariableInput})} style={btn}>
                  <Text style={btnText}>Predict Step</Text>
                </Pressable>
                <Pressable onPress={() => sendToUnity('topic.action', {topicId: 'memory', action: 'Reset'})} style={btn}>
                  <Text style={btnText}>Reset Memory Variables</Text>
                </Pressable>
              </>
            )}
            <Pressable onPress={() => sendToUnity('topic.open', {topicId: 'loops'})} style={btn}>
              <Text style={btnText}>Open Loops</Text>
            </Pressable>
            <TextInput
              value={loopValues}
              onChangeText={setLoopValues}
              placeholder="Enter 1–6 numbers, e.g. 5,10,15"
              keyboardType="numbers-and-punctuation"
              style={{marginTop: 8, padding: 14, backgroundColor: '#fff', color: '#12142B'}}
            />
            <Pressable onPress={() => sendToUnity('topic.action', {topicId: 'loops', action: 'Set Values', value: loopValues})} style={btn}>
              <Text style={btnText}>Set loop values</Text>
            </Pressable>
            <Pressable onPress={() => sendToUnity('topic.action', {topicId: 'loops', action: 'Step'})} style={btn}>
              <Text style={btnText}>Step loop</Text>
            </Pressable>
            <Pressable onPress={() => sendToUnity('topic.action', {topicId: 'loops', action: 'Run Loop'})} style={btn}>
              <Text style={btnText}>Run loop</Text>
            </Pressable>
            <Pressable onPress={() => sendToUnity('topic.action', {topicId: 'loops', action: 'Reset'})} style={btn}>
              <Text style={btnText}>Reset loops</Text>
            </Pressable>
            <Pressable onPress={() => setShowAR(s => !s)} style={btn}>
              <Text style={btnText}>{showAR ? 'Leave AR screen' : 'Back to AR screen'}</Text>
            </Pressable>
          </ScrollView>
        </View>
      )}
    </View>
  );
}
