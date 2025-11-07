#include <jni.h>
#include <string>
#include <sstream>
#include <mutex>
#include "position.h"
#include "search.h"
#include "thread.h"
#include "uci.h"
#include "ucioption.h"
#include "bitboard.h"
#include "misc.h"

using namespace Stockfish;

namespace {
    std::unique_ptr<Position> pos;
    std::unique_ptr<StateListPtr> states;
    std::mutex engine_mutex;
    bool initialized = false;
}

extern "C" {

JNIEXPORT jstring JNICALL
Java_com_unitychess_StockfishEngine_initEngine(JNIEnv* env, jobject /* this */) {
    std::lock_guard<std::mutex> lock(engine_mutex);
    
    try {
        if (!initialized) {
            // Initialize Stockfish
            Bitboards::init();
            Position::init();
            Threads.set(1); // Single thread for mobile
            Search::clear();
            
            // Create initial position
            states = std::make_unique<StateListPtr>(new std::deque<StateInfo>(1));
            pos = std::make_unique<Position>();
            pos->set(StartFEN, false, &states->back(), Threads.main());
            
            initialized = true;
        }
        return env->NewStringUTF("Stockfish initialized successfully");
    } catch (const std::exception& e) {
        std::string error = "Error initializing: ";
        error += e.what();
        return env->NewStringUTF(error.c_str());
    }
}

JNIEXPORT jstring JNICALL
Java_com_unitychess_StockfishEngine_getBestMove(JNIEnv* env, jobject /* this */,
                                               jstring fen, jint depth, jint timeMs) {
    std::lock_guard<std::mutex> lock(engine_mutex);
    
    if (!initialized) {
        return env->NewStringUTF("e2e4"); // Fallback if not initialized
    }
    
    try {
        // Get FEN string
        const char* fenStr = env->GetStringUTFChars(fen, nullptr);
        std::string fenString(fenStr);
        env->ReleaseStringUTFChars(fen, fenStr);
        
        // Set position from FEN
        states = std::make_unique<StateListPtr>(new std::deque<StateInfo>(1));
        pos->set(fenString, false, &states->back(), Threads.main());
        
        // Setup search limits
        Search::LimitsType limits;
        limits.depth = depth > 0 ? depth : 10; // Default depth 10
        limits.movetime = timeMs > 0 ? timeMs : 1000; // Default 1 second
        
        // Start search
        Threads.start_thinking(*pos, states, limits, false);
        Threads.main()->wait_for_search_finished();
        
        // Get best move
        Move bestMove = Threads.main()->bestThread->rootMoves[0].pv[0];
        
        // Convert move to UCI string (e.g., "e2e4")
        Square from = bestMove.from_sq();
        Square to = bestMove.to_sq();
        
        std::string moveStr;
        moveStr += char('a' + (from % 8));
        moveStr += char('1' + (from / 8));
        moveStr += char('a' + (to % 8));
        moveStr += char('1' + (to / 8));
        
        // Handle promotion
        if (bestMove.type_of() == PROMOTION) {
            PieceType pt = bestMove.promotion_type();
            moveStr += " qnrb"[pt - KNIGHT];
        }
        
        return env->NewStringUTF(moveStr.c_str());
        
    } catch (const std::exception& e) {
        // Return a safe default move on error
        return env->NewStringUTF("e2e4");
    }
}

JNIEXPORT void JNICALL
Java_com_unitychess_StockfishEngine_closeEngine(JNIEnv* env, jobject /* this */) {
    std::lock_guard<std::mutex> lock(engine_mutex);
    
    if (initialized) {
        Threads.set(0);
        pos.reset();
        states.reset();
        initialized = false;
    }
}

}