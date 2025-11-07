LOCAL_PATH := $(call my-dir)

include $(CLEAR_VARS)

LOCAL_MODULE := stockfish
LOCAL_SRC_FILES := \
    benchmark.cpp \
    bitboard.cpp \
    engine.cpp \
    evaluate.cpp \
    memory.cpp \
    misc.cpp \
    movegen.cpp \
    movepick.cpp \
    position.cpp \
    score.cpp \
    search.cpp \
    thread.cpp \
    timeman.cpp \
    tt.cpp \
    tune.cpp \
    uci.cpp \
    ucioption.cpp \
    stockfish_jni.cpp

LOCAL_C_INCLUDES := $(LOCAL_PATH)
LOCAL_CPPFLAGS := -std=c++17 -O3 -DNDEBUG -fno-exceptions -fno-rtti -DUSE_PTHREADS -flto
LOCAL_LDFLAGS := -flto
LOCAL_LDLIBS := -llog -landroid

include $(BUILD_SHARED_LIBRARY)