# 3D Flappy Bird - ML Training Project

A Unity-based 3D adaptation of Flappy Bird with reinforcement learning training using Unity ML-Agents. This project implements a sophisticated multi-stage training curriculum to teach an AI agent to navigate a spherical world while avoiding obstacles.

## 🎬 Demonstration

Watch the trained agent in action:

![Trained Agent Demonstration](TrainedAgentDemonstration.gif)

*The trained AI agent successfully navigating the 3D spherical world, demonstrating learned flight patterns and obstacle avoidance behaviors.*

## Reinforcement Learning Overview

This project uses **Unity ML-Agents** with **PPO (Proximal Policy Optimization)** to train an AI agent to play a 3D version of Flappy Bird. The training employs a **curriculum learning approach** with three distinct stages, each building upon the previous one.

### Training Architecture

- **Algorithm**: PPO (Proximal Policy Optimization)
- **Framework**: Unity ML-Agents 0.30.0
- **Neural Network**: 2-layer feedforward network with 64 hidden units
- **Training Method**: Curriculum learning with progressive complexity

## Training Stages

The training follows a carefully designed curriculum with three progressive stages:

### Stage 1: Survival Training
- **Objective**: Learn basic flight mechanics without obstacles
- **Focus**: Altitude control, ceiling avoidance, smooth flight patterns
- **Rewards**: 
  - Altitude-based rewards for maintaining optimal height (25% of max altitude)
  - Ceiling avoidance penalties
  - Smooth flight encouragement
  - Flapping efficiency rewards

### Stage 2: Simple Pipes
- **Objective**: Learn basic obstacle navigation
- **Spawn Rate**: Slower pipe generation (8-second intervals)
- **Focus**: Pipe detection, lateral alignment, height matching
- **Rewards**:
  - Progress rewards for moving forward
  - Lateral alignment rewards
  - Altitude alignment with pipe centers
  - Combined survival and navigation rewards

### Stage 3: Full Game
- **Objective**: Complete game complexity
- **Spawn Rate**: Normal speed (5-second intervals)
- **Focus**: Advanced navigation, scoring, complex obstacle patterns
- **Rewards**:
  - Full navigation rewards
  - Scoring bonuses (+3.0 for successful pipe passage)
  - Death penalty (-1.0)
  - Advanced alignment and progress tracking

## Observation Space

The agent receives **9 continuous observations** that simulate a **local tangent plane** instead of the full 3D spherical world. This approach simplifies the learning process by providing the agent with a 2D representation of its immediate environment.

### Basic State (3 observations)
1. **Altitude** (normalized): Height above surface / 15f
2. **Radial Velocity** (clamped): Vertical velocity / 5f
3. **Ceiling Distance** (normalized): Distance to ceiling / 15f

### Pipe Information (6 observations, when available)
4. **Distance to Pipe**: Normalized distance ahead / 20f
5. **Lateral Offset**: Side position relative to pipe center / 10f
6. **Height Difference**: Vertical offset from pipe middle / 5f
7. **Velocity Change**: Rate of approach to pipe / 5f
8. **Relative Velocity**: Bird's velocity relative to pipe / 5f
9. **Alignment Score**: Combined lateral and height alignment

### Coordinate System
The observations use a **local tangent plane coordinate system** where:
- The bird's position defines the local reference frame
- **Forward direction**: Along the planet's rotation axis (movement direction)
- **Right direction**: Perpendicular to forward, tangent to planet surface
- **Up direction**: Radial outward from planet center

This plane-based representation allows the agent to learn navigation patterns without dealing with the complexity of full 3D spherical coordinates, making training more stable and efficient.

## Action Space

The agent has **2 discrete actions**:

1. **Flap Action** (0/1): Jump radially outward from planet surface
2. **Strafe Action** (0/1/2): 
   - 0: Move left around planet
   - 1: No lateral movement
   - 2: Move right around planet

## Training Configuration

### PPO Hyperparameters (`bird.yaml`)
```yaml
behaviors:
  Bird:
    trainer_type: ppo
    hyperparameters:
      batch_size: 512
      buffer_size: 20480
      learning_rate: 1.0e-4
      beta: 1.0e-3
      epsilon: 0.2
      lambd: 0.95
      num_epoch: 3
    network_settings:
      normalize: true
      hidden_units: 64
      num_layers: 2
    reward_signals:
      extrinsic:
        gamma: 0.99
        strength: 1.0
    max_steps: 10.0e6
    time_horizon: 256
    summary_freq: 5000
```

### Key Training Features
- **Decision Period**: 1 step (real-time decision making)
- **Time Horizon**: 256 steps per episode
- **Max Steps**: 10 million training steps
- **Normalization**: Enabled for stable learning
- **Curriculum Learning**: Automatic stage progression

## Reward System

### Survival Rewards
- **Altitude Optimization**: Exponential reward for maintaining 25% of max height
- **Ceiling Avoidance**: Penalty for getting too close to ceiling
- **Smooth Flight**: Reward for stable velocity
- **Efficient Flapping**: Small reward for not over-flapping

### Navigation Rewards
- **Progress Rewards**: Forward movement toward pipes
- **Alignment Rewards**: Lateral and height alignment with pipe centers
- **Scoring Bonus**: +3.0 for successful pipe passage
- **Death Penalty**: -1.0 for collisions

### Anti-Camping Measures
- **Step Penalty**: -0.001 per step to encourage movement
- **Consecutive Flap Penalty**: Discourages excessive flapping

## Getting Started

### Prerequisites
- Unity 2022.3 LTS
- Python 3.8+ with ML-Agents
- Required packages in `requirements.txt`

### Training Setup
1. **Install ML-Agents**:
   ```bash
   pip install -r requirements.txt
   ```

2. **Configure Training**:
   - Set training stage in `BirdAgent.cs`
   - Adjust hyperparameters in `bird.yaml`
   - Configure reward weights as needed

3. **Start Training**:
   ```bash
   mlagents-learn bird.yaml --run-id=bird-training
   ```

4. **Monitor Progress**:
   ```bash
   tensorboard --logdir=results/
   ```

### Inference Mode
- Load trained `.onnx` model in Unity
- Set `BehaviorType` to `InferenceOnly`
- Deploy for gameplay

